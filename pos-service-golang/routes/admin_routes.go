package routes

import (
	handlers "github.com/Delivergate-Dev/pos-service-golang/handlers/admin"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/Delivergate-Dev/pos-service-golang/service"
	"github.com/gofiber/fiber/v2"
)

func setupAdminRoutes(router fiber.Router) {
	migrationsHandler := handlers.NewMigrationsHandler(service.NewMigrationsService())
	router.Get("/run_migrate", migrationsHandler.ApplyMigrations)

	admin := router.Group("/admin")
	admin.Post("/activity", middleware.TenantMiddleware, middleware.AuthMiddleware, handlers.NewActivityHandler().LogActivity)
}
