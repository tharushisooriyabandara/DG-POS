package transaction

import (
	"context"
	"database/sql"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
)

func UpdateOrderStatus(order *convert.Order, status string, reason string) queryFunc {
	return func(ctx context.Context, qtx *db.Queries) error {
		// update order status
		if err := qtx.UpdateOrderStatus(ctx, &db.UpdateOrderStatusParams{
			Status: status,
			ID:     order.ID,
		}); err != nil {
			return err
		}

		// update order items status
		if err := qtx.UpdateOrderItemsStatus(ctx, &db.UpdateOrderItemsStatusParams{
			Status:  status,
			OrderID: int32(order.ID),
		}); err != nil {
			return err
		}

		// update cancelled reason, if any
		if err := qtx.UpdateOrderCancelledReason(ctx, &db.UpdateOrderCancelledReasonParams{
			CancelledReason: sql.NullString{String: reason, Valid: reason != ""},
			ID:              order.ID,
		}); err != nil {
			return err
		}

		// update order timestamps
		if err := qtx.UpdateOrderTimestamps(ctx,
			convert.OrderTimestampsToOrderTimestampsParams(int32(order.ID), order.OrderTimestamps),
		); err != nil {
			return err
		}

		return nil

	}
}
