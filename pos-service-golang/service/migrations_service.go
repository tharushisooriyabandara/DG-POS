package service

import (
	"context"

	"github.com/Delivergate-Dev/pos-service-golang/database"
	"github.com/Delivergate-Dev/pos-service-golang/logger"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"go.uber.org/zap"
)

type MigrationsService struct{}

func NewMigrationsService() *MigrationsService {
	return &MigrationsService{}
}

func (s *MigrationsService) ApplyMigrations(ctx context.Context, req types.RunMigrationsRequest) error {

	if !req.RunForAllTenants {
		if err := database.RunMigrationForTenant(ctx, req.TenantCode, req.Step); err != nil {
			logger.Error("Failed to run migrations", zap.Error(err))
			return err
		}
		return nil
	}

	if err := database.RunTenantMigrations(ctx, req.Step); err != nil {
		logger.Error("Failed to run migrations", zap.Error(err))
		return err
	}
	return nil
}
