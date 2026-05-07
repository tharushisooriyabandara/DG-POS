package routes

import (
	shiftinfo "github.com/Delivergate-Dev/pos-service-golang/handlers/shift-info"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/gofiber/fiber/v2"
)

func setupShiftInfoRoutes(router fiber.Router) {
	shiftInfoHandler := shiftinfo.NewShiftInfoHandler()
	shiftInfo := router.Group("/shift-info", middleware.TenantMiddleware, middleware.AuthMiddleware)

	shiftInfo.Get("/user/:userId", shiftInfoHandler.GetShiftInfo)
	shiftInfo.Get("/shop/:shopId", shiftInfoHandler.GetShopShiftInfo)
}
