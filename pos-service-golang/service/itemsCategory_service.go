package service

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"fmt"
	"strconv"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/env"
	"github.com/elliotchance/phpserialize"
	"go.uber.org/zap"
)

var (
	ErrShopNotFound             = errors.New("shop not found")
	ErrDeliveryPlatformNotFound = errors.New("delivery platform not found")
	ErrMainMenuNotFound         = errors.New("main menu not found")
)

type menu = *db.WebshopMenu

type ItemCategoryService struct {
	db     *sql.DB
	logger *zap.Logger
}

func NewItemCategoryService(logger *zap.Logger, db *sql.DB) *ItemCategoryService {
	return &ItemCategoryService{
		logger: logger,
		db:     db,
	}
}

func (s *ItemCategoryService) GetItemCategoriesByID(ctx context.Context, shopId, brandId, mainMenuId uint64) (json.RawMessage, error) {
	queries := db.New(s.db)

	// get shop
	shop, err := queries.GetShopByID(ctx, shopId)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, ErrShopNotFound
		}
		return nil, err
	}

	// get delivery platform
	platformId, _ := strconv.Atoi(env.Config.PosPlatformID)
	deliveryPlatform, err := queries.GetDeliveryPlatform(ctx, &db.GetDeliveryPlatformParams{
		OutletID:       sql.NullInt64{Int64: int64(shop.ID), Valid: true},
		WebshopBrandID: sql.NullInt32{Int32: int32(brandId), Valid: true},
		PlatformID:     sql.NullInt32{Int32: int32(platformId), Valid: true},
	})
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, ErrDeliveryPlatformNotFound
		}
		return nil, err
	}

	// Get current time in that timezone
	timeZone, err := time.LoadLocation(shop.Timezone)
	if err != nil {
		return nil, err
	}
	localTime := time.Now().In(timeZone)

	// get menu
	menu, err := getBestAvailableMenu(ctx, queries, localTime, shopId, mainMenuId, deliveryPlatform.ID)
	if err != nil && errors.Is(err, sql.ErrNoRows) {
		return nil, ErrMainMenuNotFound
	} else if err != nil {
		return nil, err
	}

	// decode menu
	menuBytes, err := decodeMenuV2(menu.Menu.String)
	if err != nil {
		return nil, err
	}

	// return menu
	return menuBytes, nil

}

func getBestAvailableMenu(ctx context.Context, queries *db.Queries, timeNow time.Time, shopId, mainMenuId, deliveryPlatformId uint64) (menu, error) {

	partialParams := db.GetCategoryItemsByMenuIdAndShopIdParams{
		MainMenuID:         int32(mainMenuId),
		DeliveryPlatformID: int32(deliveryPlatformId),
		OutletID:           int32(shopId),
	}

	m, err := getMenuFromCurrentDay(ctx, queries, partialParams, timeNow)
	if err != nil && errors.Is(err, sql.ErrNoRows) {
		m, err = getMenuFromPreviousDay(ctx, queries, partialParams, timeNow)
		if err != nil && errors.Is(err, sql.ErrNoRows) {
			m, err = getAnyAvailableMenu(ctx, queries, partialParams, timeNow)
		}
	}

	return m, err

}

func getMenuFromCurrentDay(ctx context.Context, q *db.Queries, params db.GetCategoryItemsByMenuIdAndShopIdParams, timeNow time.Time) (menu, error) {

	// get menu from current day at same time slot
	params.Day = sql.NullString{String: timeNow.Weekday().String(), Valid: true}
	params.TimeFrom = sql.NullString{String: timeNow.Format(time.TimeOnly), Valid: true}
	params.TimeTo = sql.NullString{String: timeNow.Format(time.TimeOnly), Valid: true}

	return q.GetCategoryItemsByMenuIdAndShopId(ctx, &params)
}

func getMenuFromPreviousDay(ctx context.Context, q *db.Queries, params db.GetCategoryItemsByMenuIdAndShopIdParams, timeNow time.Time) (menu, error) {
	// get menu from previous day before current time
	params.Day = sql.NullString{String: timeNow.Add(-24 * time.Hour).Weekday().String(), Valid: true}
	params.TimeFrom = sql.NullString{}
	params.TimeTo = sql.NullString{String: timeNow.Format(time.TimeOnly), Valid: true}

	return q.GetCategoryItemsByMenuIdAndShopId(ctx, &params)

}

func getAnyAvailableMenu(ctx context.Context, q *db.Queries, params db.GetCategoryItemsByMenuIdAndShopIdParams, _ time.Time) (menu, error) {
	// get menu from today at any time
	params.Day = sql.NullString{}
	params.TimeFrom = sql.NullString{}
	params.TimeTo = sql.NullString{}

	return q.GetCategoryItemsByMenuIdAndShopId(ctx, &params)

}

// func decodeMenu(menu string) ([]byte, error) {

// 	cmd := exec.Command("php", "-r", `
// 		$input = file_get_contents("php://stdin");
// 		$obj = unserialize($input);
// 		echo json_encode($obj);
// 	`)
// 	cmd.Stdin = strings.NewReader(menu)

// 	output, err := cmd.Output()
// 	if err != nil {
// 		return nil, fmt.Errorf("failed to execute php command: command output: %s : %w", string(output), err)
// 	}

// 	return output, nil
// }

func decodeMenuV2(menu string) ([]byte, error) {

	decoded := map[any]any{}
	err := phpserialize.Unmarshal([]byte(menu), &decoded)
	if err != nil {
		return nil, fmt.Errorf("php unserialize error: %w", err)
	}

	// remove offers
	delete(decoded, "Offers")

	strIndexed := toStringMap(decoded)

	jsonBytes, err := json.Marshal(strIndexed)
	if err != nil {
		return nil, fmt.Errorf("json marshal error: %w", err)
	}

	return jsonBytes, nil

}

func toStringMap(v any) any {
	switch val := v.(type) {
	case map[any]any:
		m := make(map[string]any)
		for k, v2 := range val {
			strKey := fmt.Sprintf("%v", k)
			m[strKey] = toStringMap(v2) // recurse
		}
		return m
	case []any:
		s := make([]any, len(val))
		for i, v2 := range val {
			s[i] = toStringMap(v2) // recurse
		}
		return s
	default:
		return val
	}
}
