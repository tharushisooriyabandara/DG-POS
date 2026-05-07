package handlers

import (
	"errors"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/service"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/Delivergate-Dev/pos-service-golang/validator"
	"github.com/gofiber/fiber/v2"
)

type UserHandler struct {
}

func NewUserHandler() *UserHandler {
	return &UserHandler{}
}

// GetUsers handles the GET /users endpoint
func (h *UserHandler) GetUsers(c *fiber.Ctx) error {
	var req types.GetUsersRequest
	if err := c.QueryParser(&req); err != nil {
		return api.BadRequest("Invalid query params", err.Error())
	}

	if err := validator.Validate(&req); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	runtime := c.Locals("tenant").(*tenant.Runtime)
	users, err := runtime.UserService.GetUsers(c.Context(), &req)
	if err != nil {
		return err
	}

	return api.Ok(c, "Users fetched successfully", users)
}

// GetUser handles the GET /users/:id endpoint
func (h *UserHandler) GetUser(c *fiber.Ctx) error {
	userID, err := c.ParamsInt("id")
	if err != nil || userID == 0 {
		return api.BadRequest("Invalid user ID", err.Error())
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)
	user, err := tenant.UserService.GetUser(c.Context(), uint64(userID))
	if err != nil {
		if errors.Is(err, service.ErrUserNotFound) {
			return api.NotFound("User not found")
		}
		return err
	}

	return api.Ok(c, "User fetched successfully", user)
}

func (h *UserHandler) GetCurrentUser(c *fiber.Ctx) error {
	tenant := c.Locals("tenant").(*tenant.Runtime)
	user := c.Locals("user").(types.SessionUser)

	currentUser, err := tenant.UserService.GetUser(c.Context(), user.ID)
	if err != nil {
		if errors.Is(err, service.ErrUserNotFound) {
			return api.NotFound("User not found")
		}
		return err
	}

	return api.Ok(c, "Current User fetched successfully", currentUser)
}

func (h *UserHandler) ChangePin(c *fiber.Ctx) error {
	userID, err := c.ParamsInt("id")
	if err != nil || userID == 0 {
		return api.BadRequest("Invalid user ID", err.Error())
	}

	var changePinRequest types.ChangePinRequest
	if err := c.BodyParser(&changePinRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	if err := validator.Validate(&changePinRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	runtime := c.Locals("tenant").(*tenant.Runtime)
	changePinRequest.UserID = int32(userID)

	if err := runtime.UserService.ChangePin(c.Context(), changePinRequest); err != nil {
		if errors.Is(err, service.ErrInvalidPin) {
			return api.BadRequest("Invalid pin", err.Error())
		}
		return err
	}

	return api.Ok(c, "Pin changed successfully", struct{}{})
}
