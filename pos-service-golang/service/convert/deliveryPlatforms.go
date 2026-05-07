package convert

import (
	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

func DBDeliveryPlatformsToDpResp(platforms []*db.GetDeliveryPlatformsRow) []*types.GetDpResponse {
	dpResp := make([]*types.GetDpResponse, len(platforms))
	for i, platform := range platforms {
		dpResp[i] = &types.GetDpResponse{
			ID:                 platform.ID,
			PlatformID:         platform.PlatformID.Int32,
			PlatformName:       platform.PlatformName.String,
			Name:               platform.Name,
			WebshopBrandID:     platform.WebshopBrandID.Int32,
			ApiUrl:             platform.ApiUrl.String,
			AuthUrl:            platform.AuthUrl.String,
			ApiParameters:      platform.ApiParameters.String,
			Logo:               platform.Logo.String,
			Status:             platform.Status,
			OutletCode:         platform.OutletCode.String,
			SiteID:             platform.SiteID.String,
			BranchID:           platform.BranchID.String,
			AccessToken:        platform.AccessToken.String,
			PrimaryColor:       platform.PrimaryColor.String,
			CanUpload:          platform.CanUpload,
			AutoAccepting:      platform.AutoAccepting,
			FranchiseID:        platform.FranchiseID,
			OutletID:           platform.OutletID.Int64,
			MenuID:             platform.MenuID.String,
			MenuUploadStatus:   platform.MenuUploadStatus.String,
			StoreStatus:        platform.StoreStatus.String,
			AvailableFrom:      platform.AvailableFrom.Time,
			TenderTypes:        platform.TenderTypes.String,
			IsMaster:           platform.IsMaster,
			ParentPlatform:     platform.ParentPlatform.Int32,
			PrepTime:           platform.PrepTime.Int32,
			OwnDriver:          platform.OwnDriver,
			MenuPublishedAt:    platform.MenuPublishedAt.Time,
			WebshopSetupStatus: platform.WebshopSetupStatus,
			HasCashPayment:     platform.HasCashPayment,
			HasCardPayment:     platform.HasCardPayment,
			SelectedMenu:       platform.SelectedMenu.Int32,
			DeletedAt:          platform.DeletedAt.Time,
			CreatedAt:          platform.CreatedAt.Time,
			UpdatedAt:          platform.UpdatedAt.Time,
		}
	}
	return dpResp
}
