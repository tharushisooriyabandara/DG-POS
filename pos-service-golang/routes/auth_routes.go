package routes

import (
	handlers "github.com/Delivergate-Dev/pos-service-golang/handlers/auth"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/gofiber/fiber/v2"
)

func setupAuthRoutes(router fiber.Router) {
	authHandler := handlers.NewAuthHandler()
	auth := router.Group("/auth", middleware.TenantMiddleware)

	auth.Post("/login", authHandler.Login)
	auth.Post("/refresh", authHandler.Refresh)
	auth.Post("/verify-pin", authHandler.VerifyPin)
	auth.Delete("/logout", middleware.AuthMiddleware, authHandler.Logout)
}
