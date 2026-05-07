package service

import (
	"context"
	"database/sql"
	"fmt"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"go.uber.org/zap"
)

type DeliveryPlatformService struct {
	db     *sql.DB
	logger *zap.Logger
}

func NewDeliveryPlatformService(logger *zap.Logger, db *sql.DB) *DeliveryPlatformService {
	return &DeliveryPlatformService{
		logger: logger,
		db:     db,
	}
}

func (s *DeliveryPlatformService) GetDeliveryPlatforms(ctx context.Context, req types.QueryFilteredRequest) ([]*types.GetDpResponse, error) {
	queries := db.New(s.db)

	platforms, err := queries.GetDeliveryPlatforms(ctx, &db.GetDeliveryPlatformsParams{
		OutletID: sql.NullInt64{Int64: req.OutletID, Valid: req.OutletID != 0},
		BrandID:  sql.NullInt32{Int32: int32(req.BrandID), Valid: req.BrandID != 0},
	})
	if err != nil {
		return nil, fmt.Errorf("failed to get delivery platforms: %w", err)
	}

	return convert.DBDeliveryPlatformsToDpResp(platforms), nil
}
