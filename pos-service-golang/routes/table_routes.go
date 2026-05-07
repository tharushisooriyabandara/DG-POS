package routes

import (
	handlers "github.com/Delivergate-Dev/pos-service-golang/handlers/tables"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/gofiber/fiber/v2"
)

func setupTableRoutes(router fiber.Router) {
	tableHandler := handlers.NewTableHandler()
	tables := router.Group("/tables", middleware.TenantMiddleware, middleware.AuthMiddleware)

	tables.Get("/", tableHandler.GetTables)
	tables.Patch("/status", tableHandler.UpdateTableStatus)
}
