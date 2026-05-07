package transaction

import (
	"context"
	"database/sql"
	"fmt"
	"strconv"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
)

func CreateOrder(order *convert.Order) queryFunc {
	switch order.ShippingMethod.String {
	case "DELIVERY":
		return Build(
			createOrder(order),
			SetOrderLocation(order),
		)

	default:
		return createOrder(order)
	}
}

func createOrder(order *convert.Order) queryFunc {
	return func(ctx context.Context, qtx *db.Queries) error {

		// create order
		orderID, err := qtx.CreateOrder(ctx, convert.OrderToOrderParams(order))
		if err != nil {
			return fmt.Errorf("failed to create order: %w", err)
		}

		order.ID = uint64(orderID)

		// create order items and modifiers and taxes
		orderItemModifiers := make(map[int32][]*convert.ModifierDetails)
		orderItemTaxes := make([]*db.OrderItemTax, 0)
		err = orderItemsTransaction(ctx, qtx, order, orderItemModifiers, &orderItemTaxes)
		if err != nil {
			return fmt.Errorf("failed to create order items: %w", err)
		}

		err = orderItemModifiersTransaction(ctx, qtx, orderItemModifiers, &orderItemTaxes)
		if err != nil {
			return fmt.Errorf("failed to create order item modifiers: %w", err)
		}

		err = orderItemTaxesTransaction(ctx, qtx, order.ID, orderItemTaxes)
		if err != nil {
			return fmt.Errorf("failed to create order item taxes: %w", err)
		}

		// order sale transaction
		err = orderSalesTransaction(ctx, qtx, order)
		if err != nil {
			return fmt.Errorf("failed to create order sale transaction: %w", err)
		}

		//  create order payments
		err = orderPaymentsTransaction(ctx, qtx, order)
		if err != nil {
			return fmt.Errorf("failed to create order payments: %w", err)
		}

		// create order commission
		if err := qtx.CreateOrderCommission(
			ctx,
			convert.OrderCommissionToOrderCommissionParams(orderID, order.OrderCommission),
		); err != nil {
			return fmt.Errorf("failed to create order commission: %w", err)
		}

		// set table status
		if err := SetTableStatus(order, "RESERVED")(ctx, qtx); err != nil {
			return err
		}

		// update order timestamps
		if err := qtx.CreateOrderStatusTimestamps(ctx, &db.CreateOrderStatusTimestampsParams{
			OrderID:   int32(orderID),
			Queue:     order.CreatedAt,
			CreatedAt: order.CreatedAt,
			UpdatedAt: order.UpdatedAt,
		}); err != nil {
			return fmt.Errorf("failed to create order status timestamps: %w", err)
		}

		// set voucher usage
		if err := setVoucherUsage(ctx, qtx, order); err != nil {
			return fmt.Errorf("failed to set voucher usage: %w", err)
		}

		// create order shop fees
		if err := createOrderShopFees(ctx, qtx, orderID, order.OrderShopFees); err != nil {
			return fmt.Errorf("failed to create order shop fees: %w", err)
		}

		// create order order taxes
		if err := createOrderTaxes(ctx, qtx, orderID, order.OrderTaxes); err != nil {
			return fmt.Errorf("failed to create order taxes: %w", err)
		}

		// delete tmp payments
		if err := qtx.DeleteDgPosTmpPayment(ctx, order.DisplayOrderID.String); err != nil {
			return fmt.Errorf("failed to delete tmp payment: %w", err)
		}

		return nil
	}
}

func createOrderTaxes(ctx context.Context, qtx *db.Queries, orderID int64, orderTaxes []*db.OrderTax) error {
	for _, orderTax := range orderTaxes {
		if err := qtx.InsertOrderTaxable(ctx, &db.InsertOrderTaxableParams{
			OrderID:       int32(orderID),
			TaxRate:       orderTax.TaxRate,
			TaxCode:       orderTax.TaxCode,
			TaxAmount:     orderTax.TaxAmount,
			TaxableAmount: orderTax.TaxableAmount,
			Type:          "SALE",
		}); err != nil {
			return err
		}
	}
	return nil
}

func createOrderShopFees(ctx context.Context, qtx *db.Queries, orderID int64, orderShopFees []*db.OrderShopFee) error {
	for _, orderShopFee := range orderShopFees {
		if err := qtx.CreateOrderShopFee(ctx, &db.CreateOrderShopFeeParams{
			OrderID:    int32(orderID),
			ShopFeeID:  orderShopFee.ShopFeeID,
			Amount:     orderShopFee.Amount,
			ShopFeeTax: orderShopFee.ShopFeeTax,
			TaxID:      orderShopFee.TaxID,
			CreatedAt:  orderShopFee.CreatedAt,
			UpdatedAt:  orderShopFee.UpdatedAt,
		}); err != nil {
			return err
		}
	}
	return nil
}

func setVoucherUsage(ctx context.Context, qtx *db.Queries, order *convert.Order) error {
	for _, vc := range order.VoucherCodes {
		if err := qtx.UpdateVoucherUsage(ctx, &db.UpdateVoucherUsageParams{
			IncrementBy: 1,
			VoucherCode: vc,
		}); err != nil {
			return err
		}

		v, err := qtx.GetVoucherIdAndType(ctx, vc)
		if err != nil {
			return err
		}

		if v.VoucherType == "common" {
			if err := qtx.CreateCustomerVoucher(ctx, &db.CreateCustomerVoucherParams{
				CustomerID: uint32(order.CustomerID.Int32),
				VoucherID:  uint32(v.ID),
				IsUsed:     true,
			}); err != nil {
				return err
			}
		}

		if v.VoucherType == "personal" {
			if err := qtx.UpdateCustomerVoucher(ctx, &db.UpdateCustomerVoucherParams{
				CustomerID: uint32(order.CustomerID.Int32),
				VoucherID:  uint32(v.ID),
				IsUsed:     true,
			}); err != nil {
				return err
			}
		}

	}

	return nil
}

func SetTableStatus(order *convert.Order, tableStatus string) queryFunc {
	return func(ctx context.Context, qtx *db.Queries) error {
		if !order.DgposTableID.Valid {
			return nil
		}

		tableID := order.DgposTableID.Int32
		switch tableStatus {
		case "SERVED":
			return qtx.UpdateTableStatus(ctx, &db.UpdateTableStatusParams{
				ID:     uint64(tableID),
				Status: tableStatus,
			})

		case "AVAILABLE":
			// update table status
			if err := qtx.UpdateTableStatus(ctx, &db.UpdateTableStatusParams{
				ID:     uint64(tableID),
				Status: tableStatus,
			}); err != nil {
				return err
			}
			// remove any ongoing orders
			return qtx.RemoveOngoingOrderForTable(ctx, uint64(tableID))

		case "RESERVED":
			if err := qtx.UpdateTableStatus(ctx, &db.UpdateTableStatusParams{
				ID:     uint64(tableID),
				Status: tableStatus,
			}); err != nil {
				return err
			}
			// add the order as an ongoing order
			return qtx.AddOngoingOrderForTable(ctx, &db.AddOngoingOrderForTableParams{
				TableID: uint64(tableID),
				OrderID: order.ID,
			})
		}

		return nil
	}
}

func SetOrderLocation(order *convert.Order) queryFunc {
	return func(ctx context.Context, qtx *db.Queries) error {
		if err := qtx.CreateDgPosOrderLocation(ctx,
			convert.OrderLocationToOrderLocationParams(int32(order.ID), order.OrderLocation),
		); err != nil {
			return fmt.Errorf("failed to create order location: %w", err)
		}
		return nil
	}
}

func orderSalesTransaction(ctx context.Context, qtx *db.Queries, order *convert.Order) error {
	for i, transaction := range order.OrderTransaction {
		tid, err := qtx.CreateOrderTransaction(ctx, &db.CreateOrderTransactionParams{
			TypeID:            order.ID,
			Type:              "ORDER",
			TransactionType:   transaction.TransactionType,
			TransactionMode:   transaction.TransactionMode,
			TransactionAmount: transaction.TransactionAmount,
			PaymentType:       transaction.PaymentType,
		})
		if err != nil {
			return err
		}

		order.OrderPayments[i].TransactionID = sql.NullInt64{Int64: tid, Valid: true}
	}
	return nil
}

func orderPaymentsTransaction(ctx context.Context, qtx *db.Queries, order *convert.Order) error {
	orderID := int32(order.ID)
	for _, payment := range order.OrderPayments {
		// update dg_pos_payments
		if err := qtx.CreateDgPosPayments(ctx, convert.OrderPaymentsToDgPosPaymentsParams(orderID, payment)); err != nil {
			return err
		}

		// update payments
		if err := qtx.CreatePayment(ctx, &db.CreatePaymentParams{
			OrderID:         orderID,
			DateTime:        payment.DateTime,
			PaymentMethodID: payment.PaymentMethodID,
			Amount:          payment.Amount,
			Tax:             order.TotalTax.String,
			Discount:        strconv.Itoa(int(order.Discount)),
			CreatedAt:       payment.CreatedAt,
			UpdatedAt:       payment.UpdatedAt,
			TransactionID:   sql.NullString{String: strconv.Itoa(0), Valid: true},
			Status:          sql.NullString{String: "CREATED", Valid: true},
		}); err != nil {
			return err
		}
	}

	return nil
}

func orderItemsTransaction(
	ctx context.Context,
	qtx *db.Queries,
	order *convert.Order,
	orderItemModifiers map[int32][]*convert.ModifierDetails,
	orderItemTaxes *[]*db.OrderItemTax,
) error {

	for _, item := range order.OrderItems {

		orderItemParams := convert.OrderItemToOrderItemParams(order.ID, item)

		// create order item
		orderItemID, err := qtx.CreateOrderItem(ctx, orderItemParams)
		if err != nil {
			return err
		}

		orderItemModifiers[int32(orderItemID)] = item.ModifierDetails

		if item.OrderItemTax == nil {
			continue

		}
		oit := *item.OrderItemTax
		oit.OrderItemID = int32(orderItemID)
		*orderItemTaxes = append(*orderItemTaxes, &oit)
	}
	return nil
}

func orderItemModifiersTransaction(ctx context.Context, qtx *db.Queries, orderItemModifiers map[int32][]*convert.ModifierDetails, orderItemTaxes *[]*db.OrderItemTax) error {

	for orderItemID, modifierDetails := range orderItemModifiers {
		for _, modifier := range modifierDetails {
			modifier.OrderItemID = orderItemID
			id, err := qtx.CreateOrderItemModifier(ctx, convert.OrderItemModifiersToOrderItemModifierParams(modifier))
			if err != nil {
				return err
			}

			if modifier.OrderItemModifierTax == nil {
				continue
			}

			oimt := *modifier.OrderItemModifierTax
			oimt.OrderItemID = orderItemID
			oimt.OrderItemModifierID = sql.NullInt32{Int32: int32(id), Valid: true}
			*orderItemTaxes = append(*orderItemTaxes, &oimt)
		}
	}

	return nil
}

func orderItemTaxesTransaction(ctx context.Context, qtx *db.Queries, orderID uint64, orderItemTaxes []*db.OrderItemTax) error {
	for _, tax := range orderItemTaxes {
		if err := qtx.CreateOrderItemTax(ctx, &db.CreateOrderItemTaxParams{
			OrderID:             int32(orderID),
			OrderItemID:         tax.OrderItemID,
			OrderItemModifierID: tax.OrderItemModifierID,
			ItemPrice:           tax.ItemPrice,
			TaxID:               tax.TaxID,
			TaxProfileID:        tax.TaxProfileID,
			TaxRuleID:           tax.TaxRuleID,
			Amount:              tax.Amount,
		}); err != nil {
			return err
		}
	}

	return nil
}

// func setTableStatus(ctx context.Context, qtx *db.Queries, tableID uint64, TableStatus string, assignedOrderID int64) error {
// 	return qtx.UpdateTableStatus(ctx, &db.UpdateTableStatusParams{
// 		ID:             tableID,
// 		Status:         TableStatus,
// 		OngoingOrderID: sql.NullInt64{Int64: assignedOrderID, Valid: assignedOrderID != 0},
// 	})
// }
