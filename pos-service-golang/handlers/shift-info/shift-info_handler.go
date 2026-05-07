package shiftinfo

import (
	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/Delivergate-Dev/pos-service-golang/validator"
	"github.com/gofiber/fiber/v2"
)

type ShiftInfoHandler struct{}

func NewShiftInfoHandler() *ShiftInfoHandler {
	return &ShiftInfoHandler{}
}

func (h *ShiftInfoHandler) GetShiftInfo(c *fiber.Ctx) error {

	userID, err := c.ParamsInt("userId")
	if err != nil || userID == 0 {
		return api.BadRequest("Invalid user ID", err.Error())
	}

	var req types.GetShiftInfoRequest
	req.UserID = int64(userID)
	if err := c.QueryParser(&req); err != nil {
		return api.BadRequest("Invalid query params", err.Error())
	}

	if err := validator.Validate(&req); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	req.Requestor = c.Locals("user").(types.SessionUser)
	tenant := c.Locals("tenant").(*tenant.Runtime)
	shiftInfo, err := tenant.ReportsService.GetShiftInfo(c.Context(), req)
	if err != nil {
		return err
	}

	return api.Ok(c, "Shift info fetched successfully", shiftInfo)
}

func (h *ShiftInfoHandler) GetShopShiftInfo(c *fiber.Ctx) error {

	shopID, err := c.ParamsInt("shopId")
	if err != nil || shopID == 0 {
		return api.BadRequest("Invalid shop ID", err.Error())
	}

	var req types.GetShopShiftInfoRequest
	req.ShopID = uint32(shopID)
	if err := c.QueryParser(&req); err != nil {
		return api.BadRequest("Invalid query params", err.Error())
	}

	if err := validator.Validate(&req); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)
	shiftInfo, err := tenant.ReportsService.GetShopShiftInfo(c.Context(), req)
	if err != nil {
		return err
	}

	return api.Ok(c, "Shift info fetched successfully", shiftInfo)
}
