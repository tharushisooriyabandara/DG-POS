package convert

import (
	"math"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

func DbShiftInfoToShiftInfoResponse(userID int64, fromDate time.Time, toDate time.Time, shiftInfo []*db.GetShiftInfoByUserRow) *types.UserShiftInfoResponse {
	shiftDetails := make([]types.UserShiftDetails, len(shiftInfo))
	for i, shift := range shiftInfo {

		var shiftDuration string
		var logoutTime *time.Time

		if shift.Logout.Valid {
			logoutTime = &shift.Logout.Time
			shiftDuration = toTimeDurationString(shift.Logout.Time.Sub(shift.Login))
		} else {
			shiftDuration = toTimeDurationString(time.Now().UTC().Sub(shift.Login))
		}

		shiftDetails[i] = types.UserShiftDetails{
			ShiftID:       shift.ShiftID,
			ActiveShift:   !shift.Logout.Valid,
			LoginTime:     shift.Login,
			LogoutTime:    logoutTime,
			ShiftDuration: shiftDuration,
			OrderCount:    shift.OrdersCreated,
			CardAmount:    parseAmount(shift.CardTotal),
			CashAmount:    parseAmount(shift.CashTotal),
			TotalAmount:   parseAmount(shift.Total),
		}
	}

	orderCount := int64(0)
	totalCardAmount := 0.0
	totalCashAmount := 0.0
	totalOrderAmount := 0.0
	for _, shift := range shiftDetails {
		orderCount += shift.OrderCount
		totalCardAmount += shift.CardAmount
		totalCashAmount += shift.CashAmount
		totalOrderAmount += shift.TotalAmount
	}

	return &types.UserShiftInfoResponse{
		UserID:           userID,
		FromDate:         fromDate,
		ToDate:           toDate,
		OrderCount:       orderCount,
		TotalCardAmount:  math.Round(totalCardAmount*100) / 100.00,
		TotalCashAmount:  math.Round(totalCashAmount*100) / 100.00,
		TotalOrderAmount: math.Round(totalOrderAmount*100) / 100.00,
		ShiftDetails:     shiftDetails,
	}
}

func DbShopShiftInfoToShopShiftInfoResponse(shopID uint32, fromDate time.Time, toDate time.Time, shiftInfo []*db.GetShiftInfoByShopRow) *types.ShopShiftInfoResponse {

	shiftDetails := make([]types.ShopShiftDetails, len(shiftInfo))
	for i, shift := range shiftInfo {
		shiftDetails[i] = types.ShopShiftDetails{
			UserID:      uint32(shift.UserID.Int32),
			OrderCount:  shift.OrdersCreated,
			CardAmount:  parseAmount(shift.TotalCardSales),
			CashAmount:  parseAmount(shift.TotalCashSales),
			TotalAmount: parseAmount(shift.TotalNetSales),
		}
	}

	orderCount := int64(0)
	totalCardAmount := 0.0
	totalCashAmount := 0.0
	totalOrderAmount := 0.0
	for _, shift := range shiftDetails {
		orderCount += shift.OrderCount
		totalCardAmount += shift.CardAmount
		totalCashAmount += shift.CashAmount
		totalOrderAmount += shift.TotalAmount
	}

	return &types.ShopShiftInfoResponse{
		ShopID:           shopID,
		FromDate:         fromDate,
		ToDate:           toDate,
		OrderCount:       orderCount,
		TotalCardAmount:  math.Round(totalCardAmount*100) / 100.00,
		TotalCashAmount:  math.Round(totalCashAmount*100) / 100.00,
		TotalOrderAmount: math.Round(totalOrderAmount*100) / 100.00,
		ShiftDetails:     shiftDetails,
	}
}
