package generate

import (
	"strconv"
	"time"

	"github.com/Delivergate-Dev/pos-service-golang/env"
	"github.com/golang-jwt/jwt/v5"
)

type TokenPair struct {
	AccessToken  *jwt.Token
	RefreshToken *jwt.Token
}

type CustomClaims struct {
	TokenType string `json:"tokenType"`
	ShopID    uint64 `json:"shopId"`
	BrandID   int32  `json:"brandId"`
	jwt.RegisteredClaims
}

type TokenData struct {
	UserID  uint64
	ShopID  uint64
	BrandID int32
}

func JwtTokenPair(data TokenData) (*TokenPair, error) {

	now := time.Now().UTC()
	key := []byte(env.Config.JWTSecret)
	accessTokenExp, _ := time.ParseDuration(env.Config.AccessTokenExp)
	refreshTokenExp, _ := time.ParseDuration(env.Config.RefreshTokenExp)

	accessToken := jwt.NewWithClaims(jwt.SigningMethodHS256, CustomClaims{
		TokenType: "access",
		ShopID:    data.ShopID,
		BrandID:   data.BrandID,
		RegisteredClaims: jwt.RegisteredClaims{
			Subject:   strconv.FormatUint(data.UserID, 10),
			IssuedAt:  jwt.NewNumericDate(now),
			ExpiresAt: jwt.NewNumericDate(now.Add(accessTokenExp)),
		}})

	signedAccessToken, err := accessToken.SignedString(key)
	if err != nil {
		return nil, err
	}

	accessToken, err = Parse(signedAccessToken, key)
	if err != nil {
		return nil, err
	}

	refreshToken := jwt.NewWithClaims(jwt.SigningMethodHS256, CustomClaims{
		TokenType: "refresh",
		ShopID:    data.ShopID,
		BrandID:   data.BrandID,
		RegisteredClaims: jwt.RegisteredClaims{
			Subject:   strconv.FormatUint(data.UserID, 10),
			IssuedAt:  jwt.NewNumericDate(now),
			ExpiresAt: jwt.NewNumericDate(now.Add(refreshTokenExp)),
		}})

	signedRefreshToken, err := refreshToken.SignedString(key)
	if err != nil {
		return nil, err
	}

	refreshToken, err = Parse(signedRefreshToken, key)
	if err != nil {
		return nil, err
	}

	return &TokenPair{
		AccessToken:  accessToken,
		RefreshToken: refreshToken,
	}, nil

}

func Parse(tokenString string, key []byte) (*jwt.Token, error) {
	token, err := jwt.ParseWithClaims(tokenString, &CustomClaims{}, func(t *jwt.Token) (interface{}, error) {
		return key, nil
	})
	if err != nil {
		return nil, err
	}

	return token, nil
}
