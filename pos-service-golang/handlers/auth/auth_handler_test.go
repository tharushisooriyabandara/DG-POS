package handlers

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/Delivergate-Dev/pos-service-golang/validator"
	"github.com/gofiber/fiber/v2"
	"github.com/stretchr/testify/assert"
)

const (
	validAccessToken    = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlblR5cGUiOiJhY2Nlc3MiLCJzdWIiOiIxMSIsImV4cCI6MTc1MDE2MTcwMywiaWF0IjoxNzUwMDc1MzAzfQ.yJ6mzbBUWINx8dsB2ipROUkBQfRU7WeOU3_NkaB3MYM"
	validRefreshToken   = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlblR5cGUiOiJyZWZyZXNoIiwic3ViIjoiMTEiLCJleHAiOjE3NTA2ODAxMDMsImlhdCI6MTc1MDA3NTMwM30.8922O61cBzwuGy1YtZnVkF287WTOmvbElG9SBpSJn0s"
	invalidAccessToken  = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlblR5cGUiOiJhY2Nlc3MiLCJzdWIiOiIxMSIsImV4cCI6MTc1MDE2MTcwMywiaWF0IjoxNzUwMDc1MzAzfQ.yJ6mzbBUWINx8dsB2ipROUkBQfRU7WeOU3_NkaB3MY"
	invalidRefreshToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlblR5cGUiOiJyZWZyZXNoIiwic3ViIjoiMTEiLCJleHAiOjE3NTA2ODAxMDMsImlhdCI6MTc1MDA3NTMwM30.8922O61cBzwuGy1YtZnVkF287WTOmvbElG9SBpSJn0"
)

type mockedAuthService struct {
	db []*db.User
}

func newMockedAuthService() *mockedAuthService {
	return &mockedAuthService{
		db: []*db.User{
			{
				ID:       1,
				Name:     "test",
				Email:    "test@test.com",
				Password: "123456",
			},
			{
				ID:       2,
				Name:     "test2",
				Email:    "test2@test.com",
				Password: "123456",
			},
		},
	}
}

func (m *mockedAuthService) Refresh(ctx context.Context, refreshToken string) (string, string, error) {

	if refreshToken == validRefreshToken {
		return "mocked-access-token", "mocked-refresh-token", nil
	}

	if refreshToken == invalidRefreshToken {
		return "", "", service.ErrInvalidToken
	}

	return "", "", nil
}

func (m *mockedAuthService) VerifyPin(ctx context.Context, verifyPinRequest types.VerifyPinRequest) error {
	return nil
}

func (m *mockedAuthService) InvalidateRefreshToken(ctx context.Context, user types.SessionUser) error {
	return nil
}

func (m *mockedAuthService) ValidateAccessToken(ctx context.Context, tokenString string) (*types.SessionUser, error) {
	return nil, nil
}

func (m *mockedAuthService) Authenticate(ctx context.Context, loginRequest types.LoginRequest) (*types.SessionUser, string, string, error) {
	// Find user by ID
	var user *db.User
	for _, u := range m.db {
		if u.Email == loginRequest.Email {
			user = u
			break
		}
	}

	if user == nil {
		return nil, "", "", service.ErrAuthenticationFailed
	}

	// In the real service, we'd compare hashed PIN
	// Here we'll just do a simple comparison with the password field
	// since we don't have a PIN field in our mocked users
	if user.Password != loginRequest.Pin {
		return nil, "", "", service.ErrAuthenticationFailed
	}

	return &types.SessionUser{ID: user.ID}, "mocked-access-token", "mocked-refresh-token", nil
}

func setupTest(_ *testing.T, authService *mockedAuthService) *fiber.App {
	app := fiber.New(fiber.Config{
		ErrorHandler: api.ErrorHandler,
	})

	validator.Init()
	handler := NewAuthHandler()

	app.Use(func(c *fiber.Ctx) error {
		c.Locals("tenant", &tenant.Runtime{
			AuthService: authService,
		})
		c.Locals("user", &db.User{ID: 1})
		return c.Next()
	})

	app.Post("/login", handler.Login)
	app.Post("/refresh", handler.Refresh)
	app.Delete("/logout", handler.Logout)

	return app

}

func TestLogin(t *testing.T) {

	app := setupTest(t, newMockedAuthService())

	t.Run("should return 400 for empty or malformed request body", func(t *testing.T) {

		testPaylods := []string{
			"",  // empty body
			"{", // malformed json
			"{notjson}",
		}

		for i, payload := range testPaylods {

			t.Log("Test case", i)

			req := httptest.NewRequest("POST", "/login", strings.NewReader(payload))
			req.Header.Add("Content-Type", "application/json")

			resp, err := app.Test(req, -1)
			assert.Nil(t, err)

			respBody, err := io.ReadAll(resp.Body)
			assert.Nil(t, err)

			var appErr api.ErrorResponse
			err = json.Unmarshal(respBody, &appErr)
			assert.Nil(t, err)

			assert.Equal(t, "Invalid request body", appErr.Message)
			assert.Equal(t, 400, resp.StatusCode)
		}

		// Test for nil body
		req := httptest.NewRequest("POST", "/login", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req, -1)
		assert.Nil(t, err)

		respBody, err := io.ReadAll(resp.Body)
		assert.Nil(t, err)

		var appErr api.ErrorResponse
		err = json.Unmarshal(respBody, &appErr)
		assert.Nil(t, err)

		assert.Equal(t, "Invalid request body", appErr.Message)
		assert.Equal(t, 400, resp.StatusCode)

	})

	t.Run("should return 400 for invalid request body", func(t *testing.T) {

		testPayloads := []string{
			`{"email": "test"}`,                              // missing password
			`{"pin": "test"}`,                                // missing username
			`{"email": "", "pin": ""}`,                       // empty username and password
			`{"email": "test", "pin": ""}`,                   // password empty
			`{"email": "test", "pin": "tes"}`,                // password too short
			`{"email": "test", "pin": "testtesttesttest"}`,   // password too long
			`{"email": "test", "pin": "abc"}`,                // password not numeric
			`{"email": "test", "pin": "123"}`,                // numeric password too short
			`{"email": "test", "pin": "1234567"}`,            // numeric password too long
			`{"email": "", "pin": "testtest"}`,               // username empty
			`{"email": "test@", "pin": "testtest"}`,          // invalid email
			`{}`,                                             // empty json
			`{"notUsername": "test", "notPassword": "test"}`, // invalid json
		}

		for i, payload := range testPayloads {

			t.Log("Test case", i)

			req := httptest.NewRequest("POST", "/login", strings.NewReader(payload))
			req.Header.Add("Content-Type", "application/json")
			resp, err := app.Test(req)
			assert.Nil(t, err)

			respBody, err := io.ReadAll(resp.Body)
			assert.Nil(t, err)

			var appErr api.ErrorResponse
			err = json.Unmarshal(respBody, &appErr)
			assert.Nil(t, err)

			assert.Equal(t, "Validation failed", appErr.Message)
			assert.Equal(t, 400, resp.StatusCode)
		}

	})

	t.Run("should return 200 for valid request body", func(t *testing.T) {

		payload := `{"email": "test@test.com", "pin": "123456"}`

		req := httptest.NewRequest("POST", "/login", strings.NewReader(payload))
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		assert.Equal(t, 200, resp.StatusCode)

	})

	t.Run("should return 401 for invalid credentials", func(t *testing.T) {

		payload := `{"email": "test@test.com", "pin": "1235"}`

		req := httptest.NewRequest("POST", "/login", strings.NewReader(payload))
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		respBody, err := io.ReadAll(resp.Body)
		assert.Nil(t, err)

		var appErr api.ErrorResponse
		err = json.Unmarshal(respBody, &appErr)
		assert.Nil(t, err)

		assert.Equal(t, "Invalid credentials", appErr.Message)
		assert.Equal(t, 401, resp.StatusCode)

	})

	t.Run("should return access and refresh token for valid credentials", func(t *testing.T) {

		payload := `{"email": "test@test.com", "pin": "123456"}`

		req := httptest.NewRequest("POST", "/login", strings.NewReader(payload))
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		respBody, err := io.ReadAll(resp.Body)
		assert.Nil(t, err)

		var appRes *api.SuccessResponse[types.LoginResponse]
		err = json.Unmarshal(respBody, &appRes)
		assert.Nil(t, err)

		assert.Equal(t, 200, resp.StatusCode)
		assert.Equal(t, "Login successful", appRes.Message)
		assert.Equal(t, "mocked-access-token", appRes.Data.AccessToken)
		assert.Equal(t, "mocked-refresh-token", appRes.Data.RefreshToken)

	})

}

func TestRefresh(t *testing.T) {
	app := setupTest(t, newMockedAuthService())

	t.Run("should return 400 for empty or malformed request body", func(t *testing.T) {

		testPaylods := []string{
			"",  // empty body
			"{", // malformed json
			"{notjson}",
		}

		for i, payload := range testPaylods {

			t.Log("Test case", i)

			req := httptest.NewRequest("POST", "/refresh", strings.NewReader(payload))
			req.Header.Add("Content-Type", "application/json")

			resp, err := app.Test(req, -1)
			assert.Nil(t, err)

			respBody, err := io.ReadAll(resp.Body)
			assert.Nil(t, err)

			var appErr api.ErrorResponse
			err = json.Unmarshal(respBody, &appErr)
			assert.Nil(t, err)

			assert.Equal(t, "Invalid request body", appErr.Message)
			assert.Equal(t, 400, resp.StatusCode)
		}

		// Test for nil body
		req := httptest.NewRequest("POST", "/refresh", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req, -1)
		assert.Nil(t, err)

		respBody, err := io.ReadAll(resp.Body)
		assert.Nil(t, err)

		var appErr api.ErrorResponse
		err = json.Unmarshal(respBody, &appErr)
		assert.Nil(t, err)

		assert.Equal(t, "Invalid request body", appErr.Message)
		assert.Equal(t, 400, resp.StatusCode)

	})

	t.Run("should return 400 for invalid request body", func(t *testing.T) {

		testPayloads := []string{
			`{"refreshToken": ""}`,        // empty refresh token
			`{}`,                          // empty json
			`{"notRefreshToken": "test"}`, // invalid json
		}

		for i, payload := range testPayloads {

			t.Log("Test case", i)

			req := httptest.NewRequest("POST", "/refresh", strings.NewReader(payload))
			req.Header.Add("Content-Type", "application/json")

			resp, err := app.Test(req)
			assert.Nil(t, err)

			respBody, err := io.ReadAll(resp.Body)
			assert.Nil(t, err)

			var appErr api.ErrorResponse
			err = json.Unmarshal(respBody, &appErr)
			assert.Nil(t, err)

			assert.Equal(t, "Validation failed", appErr.Message)
			assert.Equal(t, 400, resp.StatusCode)
		}

	})

	t.Run("should return 401 for invalid refresh token", func(t *testing.T) {

		payload := fmt.Sprintf(`{"refreshToken": "%s"}`, invalidRefreshToken)

		req := httptest.NewRequest("POST", "/refresh", strings.NewReader(payload))
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		respBody, err := io.ReadAll(resp.Body)
		assert.Nil(t, err)

		var appErr api.ErrorResponse
		err = json.Unmarshal(respBody, &appErr)
		assert.Nil(t, err)

		assert.Equal(t, "Invalid token", appErr.Message)
		assert.Equal(t, 401, resp.StatusCode)

	})

	t.Run("should return 200 for valid refresh token", func(t *testing.T) {

		payload := fmt.Sprintf(`{"refreshToken": "%s"}`, validRefreshToken)

		req := httptest.NewRequest("POST", "/refresh", strings.NewReader(payload))
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		respBody, err := io.ReadAll(resp.Body)
		assert.Nil(t, err)

		var appRes *api.SuccessResponse[types.LoginResponse]
		err = json.Unmarshal(respBody, &appRes)
		assert.Nil(t, err)

		assert.Equal(t, 200, resp.StatusCode)
		assert.Equal(t, "Token refreshed", appRes.Message)
		assert.Equal(t, "mocked-access-token", appRes.Data.AccessToken)
		assert.Equal(t, "mocked-refresh-token", appRes.Data.RefreshToken)

	})

}
