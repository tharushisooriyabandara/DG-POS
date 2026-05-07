package service

import (
	"context"
	"database/sql"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"go.uber.org/zap"
)

type ReportsService struct {
	db     *db.Queries
	logger *zap.Logger
}

func NewReportsService(logger *zap.Logger, conn *sql.DB) *ReportsService {
	return &ReportsService{
		db:     db.New(conn),
		logger: logger,
	}
}

func (r *ReportsService) GetShiftInfo(ctx context.Context, req types.GetShiftInfoRequest) (*types.UserShiftInfoResponse, error) {
	// already validated, so ignoring the error
	fromDate, _ := time.Parse(time.DateTime, req.FromDate)
	toDate, _ := time.Parse(time.DateTime, req.ToDate)

	// add 23:59:59 to the end of the day
	toDate = toDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)

	// if to date is not provided, set it to the end of the from date
	if req.ToDate == "" {
		toDate = fromDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)
	}

	// shopTimezone := r.getShopTimezone(ctx, req.Requestor.ShopID)
	// fromDate, _ = time.Parse(time.DateTime, fromDate.In(shopTimezone).Format(time.DateTime))
	// toDate, _ = time.Parse(time.DateTime, toDate.In(shopTimezone).Format(time.DateTime))

	shiftInfo, err := r.db.GetShiftInfoByUser(ctx, &db.GetShiftInfoByUserParams{
		ShopID: int32(req.Requestor.ShopID),
		UserID: sql.NullInt32{Int32: int32(req.UserID), Valid: true},
		// FromDateInShopTimezone: fromDate,
		// ToDateInShopTimezone:   toDate,
		FromCreatedAt: sql.NullTime{Time: fromDate, Valid: true},
		ToCreatedAt:   sql.NullTime{Time: toDate, Valid: true},
	})
	if err != nil {
		return nil, err
	}

	return convert.DbShiftInfoToShiftInfoResponse(req.UserID, fromDate, toDate, shiftInfo), nil

}

func (r *ReportsService) GetShopShiftInfo(ctx context.Context, req types.GetShopShiftInfoRequest) (*types.ShopShiftInfoResponse, error) {
	// already validated, so ignoring the error
	fromDate, _ := time.Parse(time.DateTime, req.FromDate)
	toDate, _ := time.Parse(time.DateTime, req.ToDate)

	// add 23:59:59 to the end of the day
	toDate = toDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)

	// if to date is not provided, set it to the end of the from date
	if req.ToDate == "" {
		toDate = fromDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)
	}

	// shopTimezone := r.getShopTimezone(ctx, uint64(req.ShopID))
	// fromDate, _ = time.Parse(time.DateTime, fromDate.In(shopTimezone).Format(time.DateTime))
	// toDate, _ = time.Parse(time.DateTime, toDate.In(shopTimezone).Format(time.DateTime))

	shiftInfo, err := r.db.GetShiftInfoByShop(ctx, &db.GetShiftInfoByShopParams{
		ShopID:        int32(req.ShopID),
		FromCreatedAt: sql.NullTime{Time: fromDate, Valid: true},
		ToCreatedAt:   sql.NullTime{Time: toDate, Valid: true},
	})
	if err != nil {
		return nil, err
	}

	return convert.DbShopShiftInfoToShopShiftInfoResponse(req.ShopID, fromDate.UTC(), toDate.UTC(), shiftInfo), nil
}

func (r *ReportsService) getShopTimezone(ctx context.Context, shopId uint64) *time.Location {
	shop, _ := r.db.GetShopByID(ctx, shopId)
	loc, _ := time.LoadLocation(shop.Timezone)
	return loc
}
