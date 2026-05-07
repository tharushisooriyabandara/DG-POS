package service

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"strconv"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/env"
	"github.com/Delivergate-Dev/pos-service-golang/logger"
	"github.com/Delivergate-Dev/pos-service-golang/service/crypt"
	"github.com/Delivergate-Dev/pos-service-golang/service/generate"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/golang-jwt/jwt/v5"
	"go.uber.org/zap"
	"golang.org/x/crypto/bcrypt"
)

var (
	ErrAuthenticationFailed = errors.New("authentication failed")
	ErrInvalidToken         = errors.New("invalid token")
)

type AuthService struct {
	logger *zap.Logger
	db     *sql.DB
}

func NewAuthService(db *sql.DB, logger *zap.Logger) *AuthService {
	return &AuthService{
		logger: logger,
		db:     db,
	}
}

// Authenticate authenticates a user and returns an access token and a refresh token. it returns ErrAuthenticationFailed if the authentication fails.
func (s *AuthService) Authenticate(ctx context.Context, loginReq types.LoginRequest) (*types.SessionUser, string, string, error) {
	queries := db.New(s.db)

	user, err := queries.GetUserByOutletCodeAndEmail(ctx, &db.GetUserByOutletCodeAndEmailParams{
		HashedEmail: sql.NullString{String: crypt.ToHashed(loginReq.Email), Valid: true},
		OutletCode:  loginReq.OutletCode,
	})
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, "", "", fmt.Errorf("%w : user not found", ErrAuthenticationFailed)
		}
		logger.Error("Failed to get user", zap.Error(err))
		return nil, "", "", fmt.Errorf("failed to get user: %w", err)
	}

	if err := bcrypt.CompareHashAndPassword([]byte(user.Pin.String), []byte(loginReq.Pin)); err != nil {
		return nil, "", "", fmt.Errorf("%w : invalid credentials", ErrAuthenticationFailed)
	}

	if user.Status.String != "Active" {
		return nil, "", "", fmt.Errorf("%w : inactive user", ErrAuthenticationFailed)
	}

	access, refresh, err := generateTokens(ctx, queries, generate.TokenData{
		UserID:  user.ID,
		ShopID:  user.OutletID,
		BrandID: loginReq.BrandID,
	})
	if err != nil {
		return nil, "", "", fmt.Errorf("failed to generate tokens: %w", err)
	}

	createCashDrawerForShopIfNotExists(ctx, queries, user.OutletID)

	if err := startShift(ctx, queries, int64(user.ID)); err != nil {
		return nil, "", "", fmt.Errorf("failed to create login timestamp: %w", err)
	}

	return &types.SessionUser{
		ID:      user.ID,
		ShopID:  user.OutletID,
		BrandID: loginReq.BrandID,
	}, access, refresh, nil
}

// Refresh refreshes an access token and returns a new access token and a new refresh token. it returns ErrInvalidToken if the refresh token is invalid.
func (s *AuthService) Refresh(ctx context.Context, refreshToken string) (string, string, error) {
	token, err := generate.Parse(refreshToken, []byte(env.Config.JWTSecret))
	if err != nil {
		return "", "", fmt.Errorf("%w : failed to parse refresh token : %v", ErrInvalidToken, err)
	}

	if !token.Valid {
		return "", "", ErrInvalidToken
	}

	claims, ok := token.Claims.(*generate.CustomClaims)
	if !ok {
		return "", "", fmt.Errorf("%w : failed to get claims from refresh token", ErrInvalidToken)
	}

	if claims.TokenType != "refresh" {
		return "", "", fmt.Errorf("%w : invalid token type", ErrInvalidToken)
	}

	userId, err := claims.GetSubject()
	if err != nil {
		return "", "", fmt.Errorf("%w : failed to get user id from refresh token", ErrInvalidToken)
	}

	userID, err := strconv.ParseUint(userId, 10, 64)
	if err != nil {
		return "", "", fmt.Errorf("%w : failed to parse user id from refresh token", ErrInvalidToken)
	}

	queries := db.New(s.db)

	currentRefreshTokenRecord, err := queries.GetRefreshToken(ctx, userID)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return "", "", fmt.Errorf("%w : refresh token not found, please login again", ErrInvalidToken)
		}
		logger.Error("Failed to get refresh token", zap.Error(err))
		return "", "", fmt.Errorf("failed to get refresh token: %w", err)
	}

	if currentRefreshTokenRecord.Token != refreshToken {
		return "", "", fmt.Errorf("%w : refresh token mismatch", ErrInvalidToken)
	}

	if currentRefreshTokenRecord.ExpiresAt.Before(time.Now().UTC()) {
		return "", "", fmt.Errorf("%w : refresh token expired, please login again", ErrInvalidToken)
	}

	// generate new tokens
	return generateTokens(ctx, queries, generate.TokenData{
		UserID:  userID,
		ShopID:  claims.ShopID,
		BrandID: claims.BrandID,
	})
}

// ValidateAccessToken validates an access token and returns the user if the token is valid. it returns ErrInvalidToken if the token is invalid.
func (s *AuthService) ValidateAccessToken(ctx context.Context, tokenString string) (*types.SessionUser, error) {

	token, err := generate.Parse(tokenString, []byte(env.Config.JWTSecret))
	if err != nil {
		if errors.Is(err, jwt.ErrTokenExpired) {
			return nil, fmt.Errorf("%w : access token expired, please refresh", ErrInvalidToken)
		}
		return nil, fmt.Errorf("%w : %v", ErrInvalidToken, err.Error())
	}

	claims, ok := token.Claims.(*generate.CustomClaims)
	if !ok {
		return nil, fmt.Errorf("%w : failed to get claims from access token", ErrInvalidToken)
	}

	if claims.TokenType != "access" {
		return nil, fmt.Errorf("%w : invalid token type", ErrInvalidToken)
	}

	userID, err := strconv.ParseUint(claims.Subject, 10, 64)
	if err != nil {
		return nil, fmt.Errorf("%w : failed to parse user id from access token", ErrInvalidToken)
	}

	if userID == 0 {
		return nil, fmt.Errorf("%w : invalid user id", ErrInvalidToken)
	}

	queries := db.New(s.db)

	// check if user is logged out
	// there is no refresh token if the user is logged out
	_, err = queries.GetRefreshToken(ctx, userID)
	if err != nil {
		return nil, fmt.Errorf("%w : user is logged out, please login again", ErrInvalidToken)
	}

	user, err := queries.GetUserByID(ctx, userID)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, fmt.Errorf("%w : user not found", ErrInvalidToken)
		}
		logger.Error("Failed to get user", zap.Error(err))
		return nil, fmt.Errorf("failed to get user: %w", err)
	}

	return &types.SessionUser{
		ID:      user.ID,
		ShopID:  claims.ShopID,
		BrandID: claims.BrandID,
	}, nil
}

func (s *AuthService) InvalidateRefreshToken(ctx context.Context, user types.SessionUser) error {
	queries := db.New(s.db)

	if err := queries.DeleteRefreshToken(ctx, user.ID); err != nil {
		logger.Error("Failed to delete refresh token", zap.Error(err))
		return fmt.Errorf("failed to delete refresh token: %w", err)
	}

	if err := endShift(ctx, queries, int64(user.ID)); err != nil {
		return fmt.Errorf("failed to create logout timestamp: %w", err)
	}

	return nil
}

func (s *AuthService) VerifyPin(ctx context.Context, req types.VerifyPinRequest) error {
	queries := db.New(s.db)

	user, err := queries.GetUserByOutletCodeAndEmail(ctx, &db.GetUserByOutletCodeAndEmailParams{
		HashedEmail: sql.NullString{String: crypt.ToHashed(req.Email), Valid: true},
		OutletCode:  req.OutletCode,
	})
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return fmt.Errorf("%w : user not found", ErrAuthenticationFailed)
		}
		return fmt.Errorf("failed to get user: %w", err)
	}

	if err := bcrypt.CompareHashAndPassword([]byte(user.Pin.String), []byte(req.Pin)); err != nil {
		return fmt.Errorf("%w : invalid credentials", ErrAuthenticationFailed)
	}

	if user.Status.String != "Active" {
		return fmt.Errorf("%w : inactive user", ErrAuthenticationFailed)
	}

	return nil
}

// generateTokens generates a new access token and a new refresh token, removing the old refresh token from the database.
func generateTokens(ctx context.Context, queries *db.Queries, data generate.TokenData) (string, string, error) {

	// delete old refresh token
	if err := queries.DeleteRefreshToken(ctx, data.UserID); err != nil {
		return "", "", fmt.Errorf("failed to delete refresh token: %w", err)
	}

	// generate new pair
	tokenPair, err := generate.JwtTokenPair(data)
	if err != nil {
		return "", "", fmt.Errorf("failed to generate token pair: %w", err)
	}

	// save new refresh token to database
	claims := tokenPair.RefreshToken.Claims.(*generate.CustomClaims)
	if err := queries.CreateRefreshToken(ctx, &db.CreateRefreshTokenParams{
		UserID:    data.UserID,
		Token:     tokenPair.RefreshToken.Raw,
		ExpiresAt: claims.ExpiresAt.Time,
	}); err != nil {
		return "", "", fmt.Errorf("failed to create refresh token: %w", err)
	}

	return tokenPair.AccessToken.Raw, tokenPair.RefreshToken.Raw, nil
}

func startShift(ctx context.Context, queries *db.Queries, userID int64) error {

	// logout old shift, if any
	if err := endShift(ctx, queries, userID); err != nil {
		return err
	}

	// start new shift
	if err := queries.CreateLogin(ctx, &db.CreateLoginParams{
		UserID: userID,
		Login:  time.Now().UTC(),
	}); err != nil {
		return err
	}
	return nil
}

func endShift(ctx context.Context, queries *db.Queries, userID int64) error {
	if err := queries.SetLogout(ctx, &db.SetLogoutParams{
		UserID: userID,
		Logout: sql.NullTime{Time: time.Now().UTC(), Valid: true},
	}); err != nil {
		return err
	}
	return nil
}

func createCashDrawerForShopIfNotExists(ctx context.Context, queries *db.Queries, shopId uint64) {
	cashDrawer, _ := queries.GetCashDrawerByOutletID(ctx, shopId)
	if cashDrawer.ID != 0 {
		return
	}

	queries.CreateCashDrawer(ctx, &db.CreateCashDrawerParams{
		OutletID: shopId,
		IsActive: true,
	})
}
