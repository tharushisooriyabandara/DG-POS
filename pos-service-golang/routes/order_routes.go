package routes

import (
	handlers "github.com/Delivergate-Dev/pos-service-golang/handlers/orders"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/gofiber/fiber/v2"
)

func setupOrderRoutes(router fiber.Router) {
	orderHandler := handlers.NewOrdersHandler()
	orders := router.Group("/orders",
		middleware.TenantMiddleware,
		middleware.AuthMiddleware,
	)

	orders.Get("/", orderHandler.GetOrders)
	orders.Get("/export", orderHandler.GetOrdersExport)
	orders.Get("/:id", orderHandler.GetOrder)
	orders.Post("/", orderHandler.CreateOrder)
	orders.Put("/:id", orderHandler.UpdateOrder)
	orders.Patch("/:id/status", orderHandler.UpdateOrderStatus)
	orders.Patch("/:id/payment", orderHandler.CompleteOrderWithPayment)
	orders.Patch("/:id/refund", orderHandler.RefundOrder)

	incomingOrders := router.Group("/incoming-orders",
		middleware.TenantMiddleware,
		middleware.AuthMiddleware,
	)

	incomingOrders.Patch("/:id/user-id", orderHandler.UpdateOrderUserID)
}
