package service

import (
	"context"
	"database/sql"
	"errors"
	"fmt"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"go.uber.org/zap"
)

var (
	ErrTableNotFound = errors.New("table not found")
)

type TableService struct {
	db     *db.Queries
	logger *zap.Logger
}

func NewTableService(logger *zap.Logger, conn *sql.DB) *TableService {
	return &TableService{
		logger: logger,
		db:     db.New(conn),
	}
}

func (t *TableService) GetTables(ctx context.Context, req types.QueryFilteredRequest) ([]*types.GetTablesResponse, error) {
	tables, err := t.db.GetTables(ctx, &db.GetTablesParams{
		OutletID: sql.NullInt32{Int32: int32(req.OutletID), Valid: req.OutletID != 0},
		BrandID:  sql.NullInt32{Int32: int32(req.BrandID), Valid: req.BrandID != 0},
	})
	if err != nil {
		return nil, err
	}

	resps := convert.DbTablesRowToGetTablesResponse(tables)
	for _, table := range resps {
		ongoingOrders, err := t.db.GetOngoingOrdersForTable(ctx, table.ID)
		if err == nil {
			table.OngoingOrders = convert.ToTableOngoingOrdersResponse(ongoingOrders)
		}
		if len(ongoingOrders) > 0 {
			order, err := t.db.GetOrderById(ctx, ongoingOrders[0].OrderID)
			if err == nil {
				table.Order = convert.DbOrderToGetOrderResponse(order)
			}
		}
	}

	return resps, nil
}

func (t *TableService) UpdateTableStatus(ctx context.Context, req *types.UpdateTableStatusRequest) error {
	switch req.Status {
	case "AVAILABLE":
		table, err := t.db.GetTableByTableOrderingsID(ctx, sql.NullInt64{Int64: int64(req.TableID), Valid: true})
		if err != nil {
			return fmt.Errorf("failed to get table: %w", err)
		}

		if err := t.db.UpdateTableStatus(ctx, &db.UpdateTableStatusParams{
			ID:     table.ID,
			Status: req.Status,
		}); err != nil {
			return err
		}
		if err := t.db.RemoveOngoingOrderForTable(ctx, table.ID); err != nil {
			return err
		}

	case "RESERVED":
		table, err := t.db.GetTableByID(ctx, req.TableID)
		if err != nil {
			if errors.Is(err, sql.ErrNoRows) {
				return ErrTableNotFound
			}
			return err
		}

		if err := t.db.UpdateOrderTableId(ctx, &db.UpdateOrderTableIdParams{
			DgposTableID: sql.NullInt32{Int32: int32(table.ID), Valid: true},
			ID:           req.OngoingOrderID,
		}); err != nil {
			return fmt.Errorf("failed to update order table: %w", err)
		}

		if err := t.db.UpdateTableStatus(ctx, &db.UpdateTableStatusParams{
			ID:     table.ID,
			Status: req.Status,
		}); err != nil {
			return err
		}

		if err := t.db.AddOngoingOrderForTable(ctx, &db.AddOngoingOrderForTableParams{
			TableID: table.ID,
			OrderID: req.OngoingOrderID,
		}); err != nil {
			return err
		}

	default:
		return fmt.Errorf("invalid table status: %s", req.Status)
	}

	return nil
}
