package middleware

import (
	"errors"
	"strings"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/service"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/utils"
)

func AuthMiddleware(c *fiber.Ctx) error {

	auth := utils.CopyString(c.Get("Authorization"))
	if auth == "" {
		return api.Unauthorized("Missing Authorization header")
	}

	if !strings.HasPrefix(auth, "Bearer ") {
		return api.Unauthorized("Invalid Authorization header")
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)

	tokenStr := strings.TrimPrefix(auth, "Bearer ")
	user, err := tenant.AuthService.ValidateAccessToken(c.Context(), tokenStr)
	if err != nil {
		if errors.Is(err, service.ErrInvalidToken) {
			return api.Unauthorized("Invalid token", err.Error())
		}
		return err
	}

	sessionUser := *user
	sessionUser.TenantCode = tenant.TenantCode()

	c.Locals("user", sessionUser)
	return c.Next()

}
