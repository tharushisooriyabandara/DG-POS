package routes

import (
	handlers "github.com/Delivergate-Dev/pos-service-golang/handlers/cashDrawer"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/gofiber/fiber/v2"
)

func setupCashDrawerRoutes(router fiber.Router) {
	cashDrawerHandler := handlers.NewCashDrawerHandler()
	cashDrawerRoutes := router.Group("/cash-drawer", middleware.TenantMiddleware, middleware.AuthMiddleware)

	cashDrawerRoutes.Get("/", cashDrawerHandler.GetCashDrawerSessions)
	cashDrawerRoutes.Get("/sessions/:id", cashDrawerHandler.GetCashDrawerSession)
	cashDrawerRoutes.Get("/transactions", cashDrawerHandler.GetCashDrawerTransactionHistory)
	cashDrawerRoutes.Get("/active-session", cashDrawerHandler.GetActiveCashDrawerSessionInfo)
	cashDrawerRoutes.Post("/create", cashDrawerHandler.CreateCashDrawer)
	cashDrawerRoutes.Post("/open-session", cashDrawerHandler.OpenCashDrawerSession)
	cashDrawerRoutes.Post("/close-session", cashDrawerHandler.CloseCashDrawerSession)
	cashDrawerRoutes.Post("/record-movement", cashDrawerHandler.RecordCashMovement)
}
