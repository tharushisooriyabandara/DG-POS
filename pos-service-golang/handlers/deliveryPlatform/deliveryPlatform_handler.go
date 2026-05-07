package handlers

import (
	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/gofiber/fiber/v2"
)

type DeliveryPlatformHandler struct{}

func NewDeliveryPlatformHandler() *DeliveryPlatformHandler {
	return &DeliveryPlatformHandler{}
}

func (h *DeliveryPlatformHandler) GetDeliveryPlatforms(c *fiber.Ctx) error {

	var req types.QueryFilteredRequest
	if err := c.QueryParser(&req); err != nil {
		return api.BadRequest("Invalid query params", err.Error())
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)
	platforms, err := tenant.DeliveryPlatformService.GetDeliveryPlatforms(c.Context(), req)
	if err != nil {
		return err
	}

	return api.Ok(c, "Delivery platforms fetched successfully", platforms)
}
