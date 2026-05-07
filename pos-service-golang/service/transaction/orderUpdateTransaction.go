package transaction

import (
	"context"
	"database/sql"
	"fmt"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
	"github.com/Delivergate-Dev/pos-service-golang/service/generate"
)

func OrderPaymentUpdate(order *convert.Order) queryFunc {
	return func(ctx context.Context, qtx *db.Queries) error {

		// update order payment method
		if err := qtx.UpdateOrderPaymentMode(ctx, &db.UpdateOrderPaymentModeParams{
			PaymentMode: order.PaymentMode,
			ID:          order.ID,
		}); err != nil {
			return fmt.Errorf("failed to update order payment method: %w", err)
		}

		// update order sales transactions
		for i, transaction := range order.OrderTransaction {
			salesTransID, err := qtx.CreateOrderTransaction(ctx, &db.CreateOrderTransactionParams{
				TypeID:            order.ID,
				Type:              "ORDER",
				TransactionType:   transaction.TransactionType,
				TransactionMode:   transaction.TransactionMode,
				TransactionAmount: transaction.TransactionAmount,
				PaymentType:       transaction.PaymentType,
			})
			if err != nil {
				return fmt.Errorf("failed to create order sale transaction: %w", err)
			}
			order.OrderPayments[i].TransactionID = sql.NullInt64{Int64: salesTransID, Valid: true}
		}

		// update order payments
		if err := updateOrderPayments(ctx, qtx, order); err != nil {
			return fmt.Errorf("failed to update order payments: %w", err)
		}

		// update order status
		if err := qtx.UpdateOrderStatus(ctx, &db.UpdateOrderStatusParams{
			Status: "COMPLETED",
			ID:     order.ID,
		}); err != nil {
			return fmt.Errorf("failed to update order: %w", err)
		}

		// update order items status
		if err := qtx.UpdateOrderItemsStatus(ctx, &db.UpdateOrderItemsStatusParams{
			Status:  "COMPLETED",
			OrderID: int32(order.ID),
		}); err != nil {
			return fmt.Errorf("failed to update order items: %w", err)
		}

		// update table status
		if err := SetTableStatus(order, "AVAILABLE")(ctx, qtx); err != nil {
			return fmt.Errorf("failed to update table status: %w", err)
		}

		// legacy - remove
		// tableID, _ := strconv.Atoi(order.TableID.String)
		// if err := setTableStatus(ctx, qtx, uint64(tableID), "AVAILABLE", 0); err != nil {
		// 	return fmt.Errorf("failed to update table status: %w", err)
		// }

		// update order timestamps
		if err := qtx.UpdateOrderTimestamps(ctx, convert.OrderTimestampsToOrderTimestampsParams(int32(order.ID), order.OrderTimestamps)); err != nil {
			return fmt.Errorf("failed to update order status timestamps: %w", err)
		}

		// delete tmp payments
		if err := qtx.DeleteDgPosTmpPayment(ctx, order.DisplayOrderID.String); err != nil {
			return fmt.Errorf("failed to delete tmp payment: %w", err)
		}

		return nil
	}
}

func OrderUpdate(existingOrder *db.Order, order *convert.Order) queryFunc {
	return func(ctx context.Context, qtx *db.Queries) error {
		// update order
		if err := qtx.UpdateOrder(ctx, convert.OrderToOrderUpdateParams(order)); err != nil {
			return fmt.Errorf("failed to update order: %w", err)
		}

		// old orderItemModifierIDs
		oldOrderItemModifierIDs, err := qtx.GetAllOrderItemIDs(ctx, int32(order.ID))
		if err != nil {
			return fmt.Errorf("failed to get order item ids: %w", err)
		}

		// orderItemModifiers (used as a buffer)
		orderItemModifiers := make(map[int32][]*convert.ModifierDetails)
		orderItemTaxes := make([]*db.OrderItemTax, 0)

		// update order items
		if err := orderItemsUpdateTransaction(ctx, qtx, order, orderItemModifiers, &orderItemTaxes); err != nil {
			return fmt.Errorf("failed to update order items: %w", err)
		}

		// update order item modifiers
		if err := orderItemModifiersUpdateTransaction(
			ctx,
			qtx,
			convert.Uint64ToInt32Slice(oldOrderItemModifierIDs),
			orderItemModifiers,
			&orderItemTaxes,
		); err != nil {
			return fmt.Errorf("failed to update order item modifiers: %w", err)
		}

		// update order item taxes
		if err := updateOrderItemTaxes(ctx, qtx, order.ID, orderItemTaxes); err != nil {
			return fmt.Errorf("failed to update order item taxes: %w", err)
		}

		// update table status
		if err := updateTableStatus(ctx, qtx, existingOrder, order); err != nil {
			return fmt.Errorf("failed to update table status: %w", err)
		}

		// update order payments
		if err := updateOrderPayments(ctx, qtx, order); err != nil {
			return fmt.Errorf("failed to update order payments: %w", err)
		}

		// update order commission
		if err := updateOrderCommission(ctx, qtx, order); err != nil {
			return fmt.Errorf("failed to update order commission: %w", err)
		}

		// update order timestamps
		if err := qtx.UpdateOrderTimestamps(ctx, convert.OrderTimestampsToOrderTimestampsParams(int32(order.ID), order.OrderTimestamps)); err != nil {
			return fmt.Errorf("failed to update order status timestamps: %w", err)
		}

		// reset older voucher usage and set new voucher usages
		if err := updateVoucherUsage(ctx, qtx, existingOrder, order); err != nil {
			return fmt.Errorf("failed to update voucher usage: %w", err)
		}

		// update order shop fees
		if err := updateOrderShopFees(ctx, qtx, order); err != nil {
			return fmt.Errorf("failed to update order shop fees: %w", err)
		}

		// update order taxes
		if err := updateOrderTaxes(ctx, qtx, order); err != nil {
			return fmt.Errorf("failed to update order taxes: %w", err)
		}

		return nil
	}
}

func orderItemsUpdateTransaction(
	ctx context.Context,
	qtx *db.Queries,
	order *convert.Order,
	orderItemModifiers map[int32][]*convert.ModifierDetails,
	orderItemTaxes *[]*db.OrderItemTax,
) error {

	// delete existing order items
	if err := qtx.DeleteOrderItems(ctx, int32(order.ID)); err != nil {
		return err
	}

	// create new order items
	err := orderItemsTransaction(ctx, qtx, order, orderItemModifiers, orderItemTaxes)
	if err != nil {
		return err
	}

	return nil
}

func orderItemModifiersUpdateTransaction(
	ctx context.Context,
	qtx *db.Queries,
	oldOrderItemModifiers []int32,
	orderItemModifiers map[int32][]*convert.ModifierDetails,
	orderItemTaxes *[]*db.OrderItemTax,
) error {

	// delete existing order item modifiers
	if err := qtx.DeleteOrderItemModifiers(ctx, oldOrderItemModifiers); err != nil {
		return err
	}

	// create new order item modifiers
	err := orderItemModifiersTransaction(ctx, qtx, orderItemModifiers, orderItemTaxes)
	if err != nil {
		return err
	}

	return nil
}

func updateOrderItemTaxes(ctx context.Context, qtx *db.Queries, orderID uint64, orderItemTaxes []*db.OrderItemTax) error {
	if err := qtx.DeleteOrderItemTaxes(ctx, int32(orderID)); err != nil {
		return err
	}

	if err := orderItemTaxesTransaction(ctx, qtx, orderID, orderItemTaxes); err != nil {
		return err
	}

	return nil
}

func updateOrderPayments(ctx context.Context, qtx *db.Queries, order *convert.Order) error {

	if len(order.OrderPayments) > 1 {
		// delete existing order payments from dg_pos_payments and payments
		if err := qtx.DeleteDgPosPayments(ctx, int32(order.ID)); err != nil {
			return err
		}
		if err := qtx.DeletePayments(ctx, int32(order.ID)); err != nil {
			return err
		}

		// create new order payments
		err := orderPaymentsTransaction(ctx, qtx, order)
		if err != nil {
			return err
		}

	} else {

		// update dg pos entry
		if err := qtx.UpdateDgPosPayment(ctx, &db.UpdateDgPosPaymentParams{
			Amount:               order.OrderPayments[0].Amount,
			Cash:                 order.OrderPayments[0].Cash,
			Balance:              order.OrderPayments[0].Balance,
			CardTransactionToken: order.OrderPayments[0].CardTransactionToken,
			TransactionID:        order.OrderPayments[0].TransactionID,
			PaymentMethodID:      order.OrderPayments[0].PaymentMethodID,
			Status:               order.OrderPayments[0].Status,
			UpdatedAt:            order.OrderPayments[0].UpdatedAt,
			OrderID:              int32(order.ID),
		}); err != nil {
			return err
		}

		// update payments entry
		if err := qtx.UpdatePayment(ctx, &db.UpdatePaymentParams{
			Amount:          order.OrderPayments[0].Amount,
			Tax:             "0.00",
			Discount:        "0.00",
			PaymentMethodID: order.OrderPayments[0].PaymentMethodID,
			UpdatedAt:       order.OrderPayments[0].UpdatedAt,
			OrderID:         int32(order.ID),
		}); err != nil {
			return err
		}

	}

	return nil
}

func updateTableStatus(ctx context.Context, qtx *db.Queries, existingOrder *db.Order, order *convert.Order) error {

	newTableID := order.DgposTableID.Int32
	oldTableID := existingOrder.DgposTableID.Int32
	if newTableID == oldTableID {
		return nil
	}

	if err := SetTableStatus(&convert.Order{Order: existingOrder}, "AVAILABLE")(ctx, qtx); err != nil {
		return err
	}

	if err := SetTableStatus(order, "RESERVED")(ctx, qtx); err != nil {
		return err
	}

	return nil
}

func updateOrderCommission(ctx context.Context, qtx *db.Queries, order *convert.Order) error {
	if err := qtx.UpdateOrderCommission(ctx, &db.UpdateOrderCommissionParams{
		Commission: order.OrderCommission.Commission,
		UpdatedAt:  order.OrderCommission.UpdatedAt,
		OrderID:    int64(order.ID),
	}); err != nil {
		return err
	}
	return nil
}

func updateVoucherUsage(ctx context.Context, qtx *db.Queries, existingOrder *db.Order, order *convert.Order) error {

	if err := resetOlderVoucherUsage(ctx, qtx, existingOrder); err != nil {
		return err
	}

	if err := setVoucherUsage(ctx, qtx, order); err != nil {
		return err
	}

	return nil
}

func resetOlderVoucherUsage(ctx context.Context, qtx *db.Queries, order *db.Order) error {

	vouchers := generate.VouchersFromSerializedString(order.Vouchers.String)

	for _, vc := range vouchers {
		if err := qtx.UpdateVoucherUsage(ctx, &db.UpdateVoucherUsageParams{
			IncrementBy: -1,
			VoucherCode: vc.VoucherCode,
		}); err != nil {
			return err
		}

		v, err := qtx.GetVoucherIdAndType(ctx, vc.VoucherCode)
		if err != nil {
			return err
		}

		if v.VoucherType == "common" {
			if err := qtx.DeleteCustomerVoucher(ctx, &db.DeleteCustomerVoucherParams{
				CustomerID: uint32(order.CustomerID.Int32),
				VoucherID:  uint32(v.ID),
			}); err != nil {
				return err
			}
		}

		if v.VoucherType == "personal" {
			if err := qtx.UpdateCustomerVoucher(ctx, &db.UpdateCustomerVoucherParams{
				CustomerID: uint32(order.CustomerID.Int32),
				VoucherID:  uint32(v.ID),
				IsUsed:     false,
			}); err != nil {
				return err
			}
		}
	}
	return nil
}

func updateOrderShopFees(ctx context.Context, qtx *db.Queries, order *convert.Order) error {
	if err := qtx.DeleteOrderShopFees(ctx, int32(order.ID)); err != nil {
		return err
	}

	if err := createOrderShopFees(ctx, qtx, int64(order.ID), order.OrderShopFees); err != nil {
		return err
	}

	return nil
}

func updateOrderTaxes(ctx context.Context, qtx *db.Queries, order *convert.Order) error {
	if err := qtx.DeleteOrderTaxes(ctx, int32(order.ID)); err != nil {
		return err
	}

	if err := createOrderTaxes(ctx, qtx, int64(order.ID), order.OrderTaxes); err != nil {
		return err
	}

	return nil
}
