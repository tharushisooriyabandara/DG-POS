package routes

import (
	handlers "github.com/Delivergate-Dev/pos-service-golang/handlers/deliveryPlatform"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/gofiber/fiber/v2"
)

func setupDeliveryPlatformRoutes(router fiber.Router) {
	dpHandler := handlers.NewDeliveryPlatformHandler()
	dp := router.Group("/delivery-platform",
		middleware.TenantMiddleware,
		middleware.AuthMiddleware,
	)

	dp.Get("/", dpHandler.GetDeliveryPlatforms)
}
