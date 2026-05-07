package handlers

import (
	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/Delivergate-Dev/pos-service-golang/validator"
	"github.com/gofiber/fiber/v2"
)

type ActivityHandler struct {
}

func NewActivityHandler() *ActivityHandler {
	return &ActivityHandler{}
}

func (h *ActivityHandler) LogActivity(c *fiber.Ctx) error {

	var logActivityRequest types.LogActivityRequest
	if err := c.BodyParser(&logActivityRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	if err := validator.Validate(&logActivityRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	logActivityRequest.Requestor = c.Locals("user").(types.SessionUser)
	runtime := c.Locals("tenant").(*tenant.Runtime)

	runtime.ActivityLogService.CreateActivity(c.Context(), &logActivityRequest)

	return api.Ok(c, "Activity logged successfully", struct{}{})
}
