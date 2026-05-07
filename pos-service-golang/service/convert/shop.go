package convert

import (
	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

func DBShopToShopResp(shop *db.Shop, brandLogo string, shopFee []*db.GetShopFeesRow) *types.GetShopResponse {

	shopFees := make([]*types.ShopFee, len(shopFee))
	for i, fee := range shopFee {
		var td *types.TaxDetailsResponse
		if fee.TaxID.Valid {
			td = &types.TaxDetailsResponse{
				TaxID:   fee.TaxID.Int32,
				TaxCode: fee.Code.String,
				TaxRate: parseFloat(fee.Rate.String),
			}
		}
		shopFees[i] = &types.ShopFee{
			ID:         fee.ID,
			Type:       fee.Type.String,
			FeeType:    fee.FeeType,
			FeeName:    fee.FeeName,
			Fee:        parseFloat(fee.Fee),
			Mandatory:  fee.Mandatory,
			TaxDetails: td,
		}
	}

	return &types.GetShopResponse{
		ID:                           shop.ID,
		Name:                         shop.Name,
		ShopLogo:                     brandLogo,
		FranchiseID:                  shop.FranchiseID,
		Code:                         shop.Code,
		Email:                        shop.Email.String,
		Address:                      shop.Address.String,
		ContactNo:                    shop.ContactNo.String,
		BusinessRegNo:                shop.BusinessRegNo.String,
		Status:                       shop.Status,
		OrderStatus:                  shop.OrderStatus,
		LastUpdatedMenu:              shop.LastUpdatedMenu.Int32,
		ServiceAvailability:          shop.ServiceAvailability.String,
		MinimumAmountForFreeDelivery: parseFloat(shop.MinimumAmountForFreeDelivery.String),
		MinimumAmountForDelivery:     parseFloat(shop.MinimumAmountForDelivery.String),
		OneTimePromotionValue:        parseFloat(shop.OneTimePromotionValue.String),
		OneTimePromotionSpendAmount:  parseFloat(shop.OneTimePromotionSpendAmount.String),
		MaximumPromotionValue:        parseFloat(shop.MaximumPromotionValue),
		OneTimePromotionType:         shop.OneTimePromotionType.String,
		Latitude:                     shop.Latitude.String,
		Longitude:                    shop.Longitude.String,
		GoogleLocationUrl:            shop.GoogleLocationUrl.String,
		CountryCode:                  shop.CountryCode.String,
		Timezone:                     shop.Timezone,
		Currency:                     shop.Currency,
		CurrencyCode:                 shop.CurrencyCode,
		DeliveryPlatformEnable:       shop.DeliveryPlatformEnable,
		HasCashPayment:               shop.HasCashPayment,
		HasCardPayment:               shop.HasCardPayment,
		DelivergateAccount:           shop.DelivergateAccount,
		IsDefault:                    shop.IsDefault,
		ShopFees:                     shopFees,
		TaxMode:                      string(shop.TaxMode),
		TaxRegNo:                     shop.TaxRegNo.String,
		CreatedAt:                    shop.CreatedAt.Time,
		UpdatedAt:                    shop.UpdatedAt.Time,
	}
}

func DBDeliveryPlatformToDpResp(platform *db.GetDeliveryPlatformRow) *types.GetDpResponse {
	if platform == nil {
		return nil
	}
	return &types.GetDpResponse{
		ID:                 platform.ID,
		PlatformID:         platform.PlatformID.Int32,
		Name:               platform.Name,
		Logo:               platform.Logo.String,
		Status:             platform.Status,
		OutletCode:         platform.OutletCode.String,
		FranchiseID:        platform.FranchiseID,
		OutletID:           platform.OutletID.Int64,
		StoreStatus:        platform.StoreStatus.String,
		AvailableFrom:      platform.AvailableFrom.Time,
		IsMaster:           platform.IsMaster,
		ParentPlatform:     platform.ParentPlatform.Int32,
		PrepTime:           platform.PrepTime.Int32,
		OwnDriver:          platform.OwnDriver,
		MenuPublishedAt:    platform.MenuPublishedAt.Time,
		WebshopSetupStatus: platform.WebshopSetupStatus,
		HasCashPayment:     platform.HasCashPayment,
		HasCardPayment:     platform.HasCardPayment,
		SelectedMenu:       platform.SelectedMenu.Int32,
		PlatformName:       platform.PlatformName.String,
		WebshopBrandID:     platform.WebshopBrandID.Int32,
		WebshopBrandName:   platform.BrandName.String,
		CreatedAt:          platform.CreatedAt.Time,
		UpdatedAt:          platform.UpdatedAt.Time,
	}
}

func DbPrinterGroupsToPrinterGroupsResponse(printerGroups []*db.PrinterGroup) []*types.PrinterGroupsResp {
	printerGroupsResp := make([]*types.PrinterGroupsResp, len(printerGroups))
	for i, printerGroup := range printerGroups {
		printerGroupsResp[i] = &types.PrinterGroupsResp{
			ID:          printerGroup.ID,
			Name:        printerGroup.Name,
			Description: printerGroup.Description.String,
			Status:      printerGroup.Status,
			CreatedAt:   printerGroup.CreatedAt.Time,
			UpdatedAt:   printerGroup.UpdatedAt.Time,
		}
	}
	return printerGroupsResp
}

func DbTaxProfilesToTaxProfilesResponse(taxProfiles []*db.GetTaxProfilesWithRulesConditionsRow) []*types.TaxProfile {
	taxProfileMap := make(map[uint64]*types.TaxProfile)
	taxRuleMap := make(map[uint64]*types.TaxRule)

	for _, row := range taxProfiles {
		// Create or get tax profile
		if _, ok := taxProfileMap[row.ID]; !ok {
			taxProfileMap[row.ID] = &types.TaxProfile{
				ID:          row.ID,
				Name:        row.Name,
				Description: row.Description.String,
				Status:      row.Status,
				CreatedAt:   row.CreatedAt.Time,
				UpdatedAt:   row.UpdatedAt.Time,
				TaxRules:    []*types.TaxRule{},
			}
		}

		// Create or get tax rule
		if _, ok := taxRuleMap[row.ID_2]; !ok {
			taxRule := &types.TaxRule{
				ID:           row.ID_2,
				TaxProfileID: row.ID,
				TaxID:        uint64(row.TaxID),
				Name:         row.Name_2,
				CreatedAt:    row.CreatedAt_2.Time,
				UpdatedAt:    row.UpdatedAt_2.Time,
				Tax: &types.Tax{
					ID:          row.ID_4,
					Name:        row.Name_3.String,
					Code:        row.Code,
					Description: row.Description_2.String,
					Rate:        parseFloat(row.Rate.String),
					Status:      row.Status_2,
					CreatedAt:   row.CreatedAt_4.Time,
					UpdatedAt:   row.UpdatedAt_4.Time,
				},
				TaxRuleConditions: []*types.TaxRuleCondition{},
			}
			taxRuleMap[row.ID_2] = taxRule
			taxProfileMap[row.ID].TaxRules = append(taxProfileMap[row.ID].TaxRules, taxRule)
		}

		// Add tax rule condition
		condition := &types.TaxRuleCondition{
			ID:             row.ID_3,
			TaxRuleID:      row.ID_2,
			ConditionType:  row.ConditionType,
			ConditionValue: row.ConditionValue.String,
			MinValue:       parseFloat(row.MinValue.String),
			MaxValue:       parseFloat(row.MaxValue.String),
			StartDate:      row.StartDate.Time,
			EndDate:        row.EndDate.Time,
			CreatedAt:      row.CreatedAt_3.Time,
			UpdatedAt:      row.UpdatedAt_3.Time,
		}
		taxRuleMap[row.ID_2].TaxRuleConditions = append(taxRuleMap[row.ID_2].TaxRuleConditions, condition)
	}

	taxProfilesResp := make([]*types.TaxProfile, 0, len(taxProfileMap))
	for _, taxProfile := range taxProfileMap {
		taxProfilesResp = append(taxProfilesResp, taxProfile)
	}
	return taxProfilesResp
}
