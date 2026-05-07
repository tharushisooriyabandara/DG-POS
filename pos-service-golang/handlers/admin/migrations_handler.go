package handlers

import (
	"context"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/gofiber/fiber/v2"
	"github.com/gofiber/fiber/v2/utils"
)

type migrationsService interface {
	ApplyMigrations(ctx context.Context, req types.RunMigrationsRequest) error
}

type MigrationsHandler struct {
	migrationsService migrationsService
}

func NewMigrationsHandler(migrationsService migrationsService) *MigrationsHandler {
	return &MigrationsHandler{
		migrationsService: migrationsService,
	}
}

func (h *MigrationsHandler) ApplyMigrations(c *fiber.Ctx) error {
	var req types.RunMigrationsRequest
	req.TenantCode = utils.CopyString(c.Get("x-tenant-code"))
	req.RunForAllTenants = req.TenantCode == ""

	if err := c.QueryParser(&req); err != nil {
		return api.BadRequest("Invalid query params", err.Error())
	}

	if err := h.migrationsService.ApplyMigrations(c.Context(), req); err != nil {
		return err
	}
	return api.Accepted(c, "Migration Request accepted", struct{}{})
}
