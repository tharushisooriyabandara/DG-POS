package service

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"strconv"
	"strings"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/env"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"go.uber.org/zap"
)

type ShopService struct {
	db     *db.Queries
	logger *zap.Logger
}

func NewShopService(logger *zap.Logger, conn *sql.DB) *ShopService {
	return &ShopService{
		logger: logger,
		db:     db.New(conn),
	}
}

func (s *ShopService) GetShopInfo(ctx context.Context, shopCode string, brandId int32) (*types.GetShopResponse, error) {
	shop, err := s.db.GetShopByCode(ctx, shopCode)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, ErrShopNotFound
		}
		return nil, err
	}

	// get delivery platform
	platformId, _ := strconv.Atoi(env.Config.PosPlatformID)
	var deliveryPlatform *db.GetDeliveryPlatformRow
	deliveryPlatform, err = s.db.GetDeliveryPlatform(ctx, &db.GetDeliveryPlatformParams{
		OutletID:       sql.NullInt64{Int64: int64(shop.ID), Valid: true},
		WebshopBrandID: sql.NullInt32{Int32: int32(brandId), Valid: true},
		PlatformID:     sql.NullInt32{Int32: int32(platformId), Valid: true},
	})
	if errors.Is(err, sql.ErrNoRows) {
		deliveryPlatform = nil
	}
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return nil, err
	}

	// get basic details
	shopLogo, err := s.db.GetShopLogo(ctx, brandId)
	if err != nil {
		return nil, err
	}

	// get shop fees
	shopFees, err := s.db.GetShopFees(ctx, &db.GetShopFeesParams{
		ShopID:         int32(shop.ID),
		WebshopBrandID: uint32(brandId),
	})
	if err != nil {
		return nil, fmt.Errorf("failed to get shop fees: %w", err)
	}

	// get tax profiles with rules and conditions
	taxProfiles, err := s.db.GetTaxProfilesWithRulesConditions(ctx)
	if err != nil {
		return nil, err
	}

	// printer groups
	printerGroups, err := s.db.GetPrinterGroupsByShopID(ctx, &db.GetPrinterGroupsByShopIDParams{
		ShopID:  int32(shop.ID),
		BrandID: brandId,
	})
	if err != nil {
		return nil, fmt.Errorf("failed to get printer groups: %w", err)
	}

	shopResp := convert.DBShopToShopResp(shop, shopLogo, shopFees)
	shopResp.Dp = convert.DBDeliveryPlatformToDpResp(deliveryPlatform)
	shopResp.TaxProfiles = convert.DbTaxProfilesToTaxProfilesResponse(taxProfiles)
	shopResp.PrinterGroups = convert.DbPrinterGroupsToPrinterGroupsResponse(printerGroups)
	return shopResp, nil
}

func (s *ShopService) GetShopConfig(ctx context.Context, req *types.GetShopConfigRequest) (*types.GetShopConfigResponse, error) {
	config, err := s.db.GetShopConfig(ctx, &db.GetShopConfigParams{
		ConfigType: strings.ToUpper(req.ConfigType),
		ShopID:     req.ShopID,
		BrandID:    req.BrandID,
		TerminalID: req.TerminalID,
	})
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, fmt.Errorf("config not found: %w", err)
		}
		return nil, err
	}

	return &types.GetShopConfigResponse{
		BrandID:    config.BrandID,
		ShopID:     config.ShopID,
		TerminalID: config.TerminalID,
		ConfigType: config.ConfigType,
		Config:     config.Data,
	}, nil
}

func (s *ShopService) UpdateShopConfig(ctx context.Context, req *types.UpdateShopConfigRequest) error {
	params := &db.UpsertShopConfigParams{
		ID:         0,
		ShopID:     req.ShopID,
		BrandID:    req.BrandID,
		TerminalID: req.TerminalID,
		ConfigType: strings.ToUpper(req.ConfigType),
		Data:       req.Data,
	}

	// check if config exists
	config, err := s.db.GetShopConfig(ctx, &db.GetShopConfigParams{
		ShopID:     req.ShopID,
		BrandID:    req.BrandID,
		TerminalID: req.TerminalID,
		ConfigType: strings.ToUpper(req.ConfigType),
	})
	if err == nil {
		params.ID = config.ID
	}

	if err := s.db.UpsertShopConfig(ctx, params); err != nil {
		return fmt.Errorf("failed to upsert shop config: %w", err)
	}

	return nil
}
