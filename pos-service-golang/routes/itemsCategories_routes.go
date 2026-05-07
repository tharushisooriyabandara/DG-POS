package routes

import (
	handlers "github.com/Delivergate-Dev/pos-service-golang/handlers/items"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/gofiber/fiber/v2"
)

func setupItemCategoriesRoutes(router fiber.Router) {
	itemsHandler := handlers.NewItemCategoriesHandler()
	items := router.Group("/main-menu",
		middleware.TenantMiddleware,
		middleware.AuthMiddleware,
	)

	items.Get("/:mainMenuId/categories/webshop-brand/:brandId/shop/:shopId", itemsHandler.GetItemCategories)
}
