package routes

import (
	handlers "github.com/Delivergate-Dev/pos-service-golang/handlers"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/gofiber/fiber/v2"
)

func setupTempPaymentRoutes(router fiber.Router) {
	tempPaymentHandler := handlers.NewTempPaymentHandler()
	tempPayments := router.Group("/temp-payments", middleware.TenantMiddleware)

	tempPayments.Post("/", tempPaymentHandler.CreateDgPosTmpPayment)
	tempPayments.Get("/:typeID", tempPaymentHandler.GetDgPosTmpPayment)
}
