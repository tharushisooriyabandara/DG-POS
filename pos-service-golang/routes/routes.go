package routes

import (
	"github.com/Delivergate-Dev/pos-service-golang/logger"
	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/middleware/cors"
	"github.com/gofiber/fiber/v2/middleware/recover"
)

// Setup configures all application routes
func Setup(app *fiber.App) {

	// Setup CORS
	app.Use(cors.New())

	// panic recover
	app.Use(recover.New())

	// Setup base API
	router := app.Group("/api/v1")

	// Setup all route groups
	setupAdminRoutes(router)
	setupUserRoutes(router)
	setupAuthRoutes(router)
	setupItemCategoriesRoutes(router)
	setupOrderRoutes(router)
	setupCustomerRoutes(router)
	setupShopInfoRoutes(router)
	setupTableRoutes(router)
	setupDeliveryPlatformRoutes(router)
	setupShiftInfoRoutes(router)
	setupCashDrawerRoutes(router)
	setupTempPaymentRoutes(router)

	// Setup health check endpoint
	app.Get("/", func(c *fiber.Ctx) error {
		logger.Info("Server is running")
		return c.SendString("Go-Fiber POS Service API is running")
	})
}
