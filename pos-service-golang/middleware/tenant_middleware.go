package middleware

import (
	"strings"

	tenantFactory "github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/utils"
)

// TenantMiddleware is a middleware function that checks the tenant code in the request header and sets the database connection for the request context.
func TenantMiddleware(c *fiber.Ctx) error {
	tenantCode := strings.ToLower(utils.CopyString(c.Get("x-tenant-code"))) // using copies, as documented in https://docs.gofiber.io/api/ctx/#get
	if tenantCode == "" {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "Missing x-tenant-code header",
		})
	}

	// Get the tenant context from the factory
	tenant, err := tenantFactory.GetRuntime(c.Context(), tenantCode)
	if err != nil {
		return c.Status(fiber.StatusUnauthorized).JSON(fiber.Map{
			"error": err.Error(),
		})
	}

	// Set the tenant code in the request context
	c.Locals("tenant", tenant)
	return c.Next()
}
