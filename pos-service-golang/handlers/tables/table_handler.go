package handlers

import (
	"errors"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/service"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/Delivergate-Dev/pos-service-golang/validator"
	"github.com/gofiber/fiber/v2"
)

type TableHandler struct {
}

func NewTableHandler() *TableHandler {
	return &TableHandler{}
}

func (h *TableHandler) GetTables(c *fiber.Ctx) error {

	var req types.QueryFilteredRequest
	if err := c.QueryParser(&req); err != nil {
		return api.BadRequest("Invalid query params", err.Error())
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)
	tables, err := tenant.TableService.GetTables(c.Context(), req)
	if err != nil {
		return err
	}

	return api.Ok(c, "Tables fetched successfully", tables)
}

func (h *TableHandler) UpdateTableStatus(c *fiber.Ctx) error {
	var req types.UpdateTableStatusRequest
	if err := c.BodyParser(&req); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	if err := validator.Validate(&req); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)
	if err := tenant.TableService.UpdateTableStatus(c.Context(), &req); err != nil {
		if errors.Is(err, service.ErrTableNotFound) {
			return api.NotFound("Table not found")
		}
		return err
	}

	return api.Ok(c, "Table status updated successfully", struct{}{})
}
