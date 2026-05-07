package handlers

import (
	"errors"
	"fmt"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/service"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/Delivergate-Dev/pos-service-golang/validator"
	"github.com/gofiber/fiber/v2"
)

type AuthHandler struct {
}

func NewAuthHandler() *AuthHandler {
	return &AuthHandler{}
}

// Login handles the POST /login endpoint
func (h *AuthHandler) Login(c *fiber.Ctx) error {
	var loginRequest types.LoginRequest
	if err := c.BodyParser(&loginRequest); err != nil {
		return api.BadRequest("Invalid request body")
	}

	if err := validator.Validate(&loginRequest); err != nil {
		return api.BadRequest("Validation failed", fmt.Sprintf(
			"%s %s",
			validator.TranslateErrors(err),
			"Please complete all required configuration fields.",
		))
	}

	runtime := c.Locals("tenant").(*tenant.Runtime)
	_, accessToken, refreshToken, err := runtime.AuthService.Authenticate(c.Context(), loginRequest)
	if err != nil {
		if errors.Is(err, service.ErrAuthenticationFailed) {
			return api.Unauthorized("Invalid credentials", err.Error())
		}
		return err
	}

	return api.Ok(c, "Login successful", &types.LoginResponse{
		AccessToken:  accessToken,
		RefreshToken: refreshToken,
	})
}

func (h *AuthHandler) Refresh(c *fiber.Ctx) error {
	var refreshRequest types.RefreshRequest
	if err := c.BodyParser(&refreshRequest); err != nil {
		return api.BadRequest("Invalid request body")
	}

	if err := validator.Validate(&refreshRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)
	accessToken, refreshToken, err := tenant.AuthService.Refresh(c.Context(), refreshRequest.RefreshToken)
	if err != nil {
		if errors.Is(err, service.ErrInvalidToken) {
			return api.Unauthorized("Invalid token", err.Error())
		}
		return err
	}

	return api.Ok(c, "Token refreshed", &types.LoginResponse{
		AccessToken:  accessToken,
		RefreshToken: refreshToken,
	})

}

func (h *AuthHandler) Logout(c *fiber.Ctx) error {
	user := c.Locals("user").(types.SessionUser)
	runtime := c.Locals("tenant").(*tenant.Runtime)

	if err := runtime.AuthService.InvalidateRefreshToken(c.Context(), user); err != nil {
		return err
	}

	return api.Ok(c, "Logout successful", struct{}{})
}

func (h *AuthHandler) VerifyPin(c *fiber.Ctx) error {
	var verifyPinRequest types.VerifyPinRequest
	if err := c.BodyParser(&verifyPinRequest); err != nil {
		return api.BadRequest("Invalid request body")
	}

	if err := validator.Validate(&verifyPinRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	runtime := c.Locals("tenant").(*tenant.Runtime)
	if err := runtime.AuthService.VerifyPin(c.Context(), verifyPinRequest); err != nil {
		if errors.Is(err, service.ErrAuthenticationFailed) {
			return api.BadRequest("Invalid credentials")
		}
		return err
	}

	return api.Ok(c, "Pin verified", struct{}{})
}
