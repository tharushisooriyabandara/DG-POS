package handlers

import (
	"errors"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/service"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/gofiber/fiber/v2"
)

type ItemCategoriesHandler struct {
}

func NewItemCategoriesHandler() *ItemCategoriesHandler {
	return &ItemCategoriesHandler{}
}

func (h *ItemCategoriesHandler) GetItemCategories(c *fiber.Ctx) error {

	mainMenuID, err := c.ParamsInt("mainMenuId")
	if err != nil || mainMenuID == 0 {
		return api.BadRequest("Invalid main menu ID", err.Error())
	}

	brandID, err := c.ParamsInt("brandId")
	if err != nil || brandID == 0 {
		return api.BadRequest("Invalid brand ID", err.Error())
	}

	shopID, err := c.ParamsInt("shopId")
	if err != nil || shopID == 0 {
		return api.BadRequest("Invalid shop ID", err.Error())
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)
	menu, err := tenant.MenuService.GetItemCategoriesByID(c.Context(), uint64(shopID), uint64(brandID), uint64(mainMenuID))
	if err != nil {
		switch {
		case errors.Is(err, service.ErrShopNotFound):
			return api.NotFound("Shop not found")
		case errors.Is(err, service.ErrDeliveryPlatformNotFound):
			return api.NotFound("Delivery platform not found")
		case errors.Is(err, service.ErrMainMenuNotFound):
			return api.NotFound("Main menu not found")
		default:
			return err
		}
	}

	return api.Ok(c, "Items fetched successfully", menu)

}
