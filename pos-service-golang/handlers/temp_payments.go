package handlers

import (
	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/utils"
)

type tempPaymentHandler struct{}

func NewTempPaymentHandler() *tempPaymentHandler {
	return &tempPaymentHandler{}
}

func (h *tempPaymentHandler) CreateDgPosTmpPayment(c *fiber.Ctx) error {
	var paymentRequest types.DgPosTmpPaymentRequest
	if err := c.BodyParser(&paymentRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	runtime := c.Locals("tenant").(*tenant.Runtime)
	if err := runtime.TemporaryPaymentService.CreateDgPosTmpPayment(c.Context(), &paymentRequest); err != nil {
		return err
	}

	return api.Ok(c, "success", struct{}{})
}

func (h *tempPaymentHandler) GetDgPosTmpPayment(c *fiber.Ctx) error {
	typeID := utils.CopyString(c.Params("typeID"))
	if typeID == "" {
		return api.BadRequest("Invalid order ID", "Type ID is required")
	}

	runtime := c.Locals("tenant").(*tenant.Runtime)
	payments, err := runtime.TemporaryPaymentService.GetDgPosTmpPayment(c.Context(), typeID)
	if err != nil {
		return err
	}

	return api.Ok(c, "success", payments)
}
