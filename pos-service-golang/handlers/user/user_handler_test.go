package handlers

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"io"
	"net/http/httptest"
	"strconv"
	"testing"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/gofiber/fiber/v2"
	"github.com/stretchr/testify/assert"
)

func setupTest(t *testing.T, userService *MockedUserService) *fiber.App {
	t.Helper()

	app := fiber.New(fiber.Config{
		ErrorHandler: api.ErrorHandler,
	})

	app.Use(func(c *fiber.Ctx) error {
		c.Locals("tenant", &tenant.Runtime{
			UserService: userService,
		})
		return c.Next()
	})

	handler := NewUserHandler()
	app.Get("/users", handler.GetUsers)
	app.Get("/users/:id", handler.GetUser)

	return app

}

func TestGetUsers(t *testing.T) {
	app := setupTest(t, NewMockedUserService())

	t.Run("should return 200 for valid request", func(t *testing.T) {
		req := httptest.NewRequest("GET", "/users", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		respBody, err := io.ReadAll(resp.Body)
		assert.Nil(t, err)

		var users api.SuccessResponse[[]types.GetUsersResponse]
		err = json.Unmarshal(respBody, &users)
		assert.Nil(t, err)

		assert.Equal(t, 200, resp.StatusCode)
		assert.Equal(t, "Users fetched successfully", users.Message)
		assert.Equal(t, 2, len(users.Data))
	})

	t.Run("should return 200 for valid request with no users", func(t *testing.T) {
		userService := NewMockedUserService()
		userService.users = []*db.GetUsersRow{}
		app := setupTest(t, userService)

		req := httptest.NewRequest("GET", "/users", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		respBody, err := io.ReadAll(resp.Body)
		assert.Nil(t, err)

		var users api.SuccessResponse[[]types.GetUsersResponse]
		err = json.Unmarshal(respBody, &users)
		assert.Nil(t, err)

		assert.Equal(t, 200, resp.StatusCode)
		assert.Equal(t, "Users fetched successfully", users.Message)
		assert.Equal(t, 0, len(users.Data))
	})

	t.Run("should return 500 for internal server error", func(t *testing.T) {
		userService := NewMockedUserService()
		userService.users = nil
		app := setupTest(t, userService)

		req := httptest.NewRequest("GET", "/users", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		assert.Equal(t, 500, resp.StatusCode)
	})
}

func TestGetUser(t *testing.T) {
	app := setupTest(t, NewMockedUserService())

	t.Run("should return 400 for invalid user id", func(t *testing.T) {
		req := httptest.NewRequest("GET", "/users/invalid", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		assert.Equal(t, 400, resp.StatusCode)
	})

	t.Run("should return 404 for user not found", func(t *testing.T) {
		req := httptest.NewRequest("GET", "/users/100", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		assert.Equal(t, 404, resp.StatusCode)
	})

	t.Run("should return 500 for internal server error", func(t *testing.T) {
		userService := NewMockedUserService()
		userService.users = nil

		app := setupTest(t, userService)

		req := httptest.NewRequest("GET", "/users/1", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		assert.Equal(t, 500, resp.StatusCode)
	})

	t.Run("should return 200 for valid request", func(t *testing.T) {
		req := httptest.NewRequest("GET", "/users/1", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		respBody, err := io.ReadAll(resp.Body)
		assert.Nil(t, err)

		var user api.SuccessResponse[types.GetUsersResponse]
		err = json.Unmarshal(respBody, &user)
		assert.Nil(t, err)

		assert.Equal(t, 200, resp.StatusCode)
		assert.Equal(t, "User fetched successfully", user.Message)
		assert.Equal(t, uint64(1), user.Data.ID)
		assert.Equal(t, "test", user.Data.FirstName)
	})
}

type MockedUserService struct {
	users []*db.GetUsersRow
}

func NewMockedUserService() *MockedUserService {
	return &MockedUserService{
		users: []*db.GetUsersRow{
			{
				ID:       1,
				Name:     "test",
				LastName: sql.NullString{String: "test", Valid: true},
				Email:    "test@test.com",
				RoleID:   sql.NullInt32{Int32: 1, Valid: true},
				RoleName: sql.NullString{String: "test", Valid: true},
			},
			{
				ID:       2,
				Name:     "test2",
				LastName: sql.NullString{String: "test2", Valid: true},
				Email:    "test2@test.com",
				RoleID:   sql.NullInt32{Int32: 2, Valid: true},
				RoleName: sql.NullString{String: "test2", Valid: true},
			},
		},
	}
}

func (s *MockedUserService) GetUser(ctx context.Context, userID uint64) (*types.GetUserResponse, error) {

	if s.users == nil {
		return nil, errors.New("server error")
	}

	if userID > uint64(len(s.users)) {
		return nil, service.ErrUserNotFound
	}

	return &types.GetUserResponse{
		ID:        s.users[userID-1].ID,
		FirstName: s.users[userID-1].Name,
		LastName:  s.users[userID-1].LastName.String,
		Email:     s.users[userID-1].Email,
		Role:      s.users[userID-1].RoleName.String,
		RoleId:    strconv.Itoa(int(s.users[userID-1].RoleID.Int32)),
	}, nil
}

func (s *MockedUserService) GetUsers(ctx context.Context, getUsersRequest *types.GetUsersRequest) ([]*types.GetUsersResponse, error) {
	if s.users == nil {
		return nil, errors.New("server error")
	}

	return convert.DBUserToUserResp(s.users), nil
}

func (s *MockedUserService) ChangePin(ctx context.Context, changePinRequest types.ChangePinRequest) error {
	return nil
}
