package handlers

import (
	"cmp"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/Delivergate-Dev/pos-service-golang/validator"
	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/utils"
)

type ShopHandler struct {
}

func NewShopHandler() *ShopHandler {
	return &ShopHandler{}
}

func (h *ShopHandler) GetShopInfo(c *fiber.Ctx) error {
	shopCode := utils.CopyString(c.Query("code"))
	if shopCode == "" {
		return api.BadRequest("invalid shop code", "Shop code is required")
	}

	brandId := c.QueryInt("brandId")
	if brandId == 0 {
		return api.BadRequest("invalid brand ID", "Brand ID is required")
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)
	shop, err := tenant.ShopService.GetShopInfo(c.Context(), shopCode, int32(brandId))
	if err != nil {
		return err
	}

	return api.Ok(c, "Shop fetched successfully", shop)
}

func (h *ShopHandler) GetShopConfig(c *fiber.Ctx) error {
	var req types.GetShopConfigRequest
	if err := c.ParamsParser(&req); err != nil {
		return api.BadRequest("Invalid request params", err.Error())
	}
	if err := c.QueryParser(&req); err != nil {
		return api.BadRequest("Invalid query params", err.Error())
	}
	if err := validator.Validate(&req); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	req.BrandID = cmp.Or(req.BrandID, 1)
	req.TerminalID = cmp.Or(req.TerminalID, 1)

	tenant := c.Locals("tenant").(*tenant.Runtime)
	config, err := tenant.ShopService.GetShopConfig(c.Context(), &req)
	if err != nil {
		return err
	}

	return api.Ok(c, "Shop config fetched successfully", config)
}

func (h *ShopHandler) UpdateShopConfig(c *fiber.Ctx) error {
	var req types.UpdateShopConfigRequest
	if err := c.ParamsParser(&req); err != nil {
		return api.BadRequest("Invalid request params", err.Error())
	}
	if err := c.QueryParser(&req); err != nil {
		return api.BadRequest("Invalid query params", err.Error())
	}
	if err := c.BodyParser(&req); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}
	if err := validator.Validate(&req); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	req.BrandID = cmp.Or(req.BrandID, 1)
	req.TerminalID = cmp.Or(req.TerminalID, 1)

	tenant := c.Locals("tenant").(*tenant.Runtime)
	if err := tenant.ShopService.UpdateShopConfig(c.Context(), &req); err != nil {
		return err
	}

	return api.Ok(c, "Shop config updated successfully", struct{}{})
}
