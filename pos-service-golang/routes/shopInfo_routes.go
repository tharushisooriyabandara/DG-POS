package routes

import (
	handlers "github.com/Delivergate-Dev/pos-service-golang/handlers/shop"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/gofiber/fiber/v2"
)

func setupShopInfoRoutes(router fiber.Router) {
	shopHandler := handlers.NewShopHandler()
	shopInfoRoute := router.Group("/shop-info",
		middleware.TenantMiddleware,
	)
	shopInfoRoute.Get("/", shopHandler.GetShopInfo)

	shop := router.Group("/shop/:shopId", middleware.TenantMiddleware)

	shopInfo := shop.Group("/info")
	shopInfo.Get("/", shopHandler.GetShopInfo)

	shopConfig := shop.Group("/config/:configType")
	shopConfig.Get("/", shopHandler.GetShopConfig)
	shopConfig.Patch("/", shopHandler.UpdateShopConfig)
}
