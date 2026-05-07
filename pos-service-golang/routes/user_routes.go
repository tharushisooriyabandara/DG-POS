package routes

import (
	handlers "github.com/Delivergate-Dev/pos-service-golang/handlers/user"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/gofiber/fiber/v2"
)

// SetupUserRoutes configures all user-related routes
func setupUserRoutes(router fiber.Router) {
	userHandler := handlers.NewUserHandler()
	users := router.Group("/users", middleware.TenantMiddleware)

	users.Get("/", userHandler.GetUsers)
	users.Get("/current", middleware.AuthMiddleware, userHandler.GetCurrentUser)
	users.Get("/:id", middleware.AuthMiddleware, userHandler.GetUser)
	users.Patch("/:id/pin", middleware.AuthMiddleware, userHandler.ChangePin)
}
