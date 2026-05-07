package service

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
	"github.com/Delivergate-Dev/pos-service-golang/service/external"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"go.uber.org/zap"
	"golang.org/x/crypto/bcrypt"
)

type userCryptoService interface {
	DecryptUserByIDRow(ctx context.Context, user *db.GetUserByIDRow) (*db.GetUserByIDRow, error)
	DecryptUsersRow(ctx context.Context, users []*db.GetUsersRow) ([]*db.GetUsersRow, error)
}

var (
	ErrUserNotFound = errors.New("user not found")
	ErrInvalidPin   = errors.New("invalid pin")
)

// userService implements IUserService interface
type userService struct {
	tenantCode string
	db         *sql.DB
	logger     *zap.Logger
	crypto     userCryptoService
}

// NewUserService creates a new user service instance
func NewUserService(tenantCode string, db *sql.DB, logger *zap.Logger, crypto userCryptoService) *userService {
	return &userService{
		logger:     logger,
		db:         db,
		crypto:     crypto,
		tenantCode: tenantCode,
	}
}

// GetUsers retrieves all users from the database
func (s *userService) GetUsers(ctx context.Context, getUsersRequest *types.GetUsersRequest) ([]*types.GetUsersResponse, error) {
	queries := db.New(s.db)

	roleIDs := []uint32{4, 5}
	if len(getUsersRequest.Roles) > 0 {
		roleIDs = getUsersRequest.Roles
	}

	users, err := queries.GetUsers(ctx, &db.GetUsersParams{
		RoleIds:    roleIDs,
		OutletCode: getUsersRequest.OutletCode,
	})
	if err != nil {
		return nil, err
	}

	decryptedUsers, err := s.crypto.DecryptUsersRow(ctx, users)
	if err != nil {
		return nil, err
	}

	return convert.DBUserToUserResp(decryptedUsers), nil
}

func (s *userService) GetUser(ctx context.Context, userID uint64) (*types.GetUserResponse, error) {
	queries := db.New(s.db)

	user, err := queries.GetUserByID(ctx, userID)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, ErrUserNotFound
		}
		return nil, err
	}

	decryptedUser, err := s.crypto.DecryptUserByIDRow(ctx, user)
	if err != nil {
		return nil, err
	}

	reportService, err := queries.GetLatestReportServiceToken(ctx)
	if err != nil {
		return nil, err
	}

	// generate a new token if the current token is expired
	bearerToken := reportService.BearerToken.String
	if time.Now().UTC().After(reportService.ExpireIn.Time) {
		var err error
		bearerToken, err = s.refreshReportBearerToken(ctx, queries, reportService)
		if err != nil {
			return nil, err
		}
	}

	userResp := convert.DBUserToUser(decryptedUser, bearerToken)

	return userResp, nil
}

func (s *userService) ChangePin(ctx context.Context, changePinRequest types.ChangePinRequest) error {
	queries := db.New(s.db)

	user, err := queries.GetUserByID(ctx, uint64(changePinRequest.UserID))
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return ErrUserNotFound
		}
		return err
	}

	// cashier should have 4 digits in the pin
	// manager should have 6 digits in the pin
	if user.RoleID.Int32 == 5 && len(changePinRequest.NewPin) != 4 {
		return fmt.Errorf("%w: pin length should be 4 for role %s", ErrInvalidPin, user.RoleName.String)
	}

	if user.RoleID.Int32 == 4 && len(changePinRequest.NewPin) != 6 {
		return fmt.Errorf("%w: pin length should be 6 for role %s", ErrInvalidPin, user.RoleName.String)
	}

	// bcrypt the new pin
	hashedPin, err := bcrypt.GenerateFromPassword([]byte(changePinRequest.NewPin), bcrypt.DefaultCost)
	if err != nil {
		return err
	}

	if err := queries.UpdateUserPin(ctx, &db.UpdateUserPinParams{
		ID:  user.ID,
		Pin: sql.NullString{String: string(hashedPin), Valid: true},
	}); err != nil {
		return err
	}

	return nil
}

func (s *userService) refreshReportBearerToken(ctx context.Context, queries *db.Queries, reportService *db.ReportService) (string, error) {
	newToken, err := external.GenerateReportServiceBearerToken(s.tenantCode, *reportService)
	if err != nil {
		return "", err
	}

	if err := queries.UpdateReportServiceToken(ctx, &db.UpdateReportServiceTokenParams{
		ID:          reportService.ID,
		BearerToken: sql.NullString{String: newToken, Valid: true},
		ExpireIn:    sql.NullTime{Time: time.Now().UTC().AddDate(5, 0, 0), Valid: true},
	}); err != nil {
		return "", err
	}

	return newToken, nil
}
