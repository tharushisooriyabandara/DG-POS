package usecase

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"strconv"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	posErr "github.com/Delivergate-Dev/pos-service-golang/errors"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
)

func VerifyCustomer(ctx context.Context, qtx *db.Queries, order *convert.Order) error {
	if order.CustomerID.Int32 == 0 {
		return nil
	}

	customer, err := qtx.GetCustomerByID(ctx, uint64(order.CustomerID.Int32))
	if err != nil {
		return fmt.Errorf("failed to get customer: %w", err)
	}

	if customer.ID == 0 {
		return fmt.Errorf("customer does not exist")
	}

	return nil
}

func PaymentAmount(ctx context.Context, qtx *db.Queries, order *convert.Order) error {

	// applied for any order that has one or more payments
	if len(order.OrderPayments) == 0 {
		return nil
	}

	var payingTotal int
	for _, payment := range order.OrderPayments {
		amount, err := strconv.Atoi(payment.Amount)
		if err != nil {
			return err
		}
		payingTotal += amount
	}

	if payingTotal != int(order.TotalAmount) {
		return fmt.Errorf(
			"paying total should match the order amount. paying total: %.2f, order amount: %.2f",
			float64(payingTotal)/100.00,
			float64(order.TotalAmount)/100.00,
		)
	}

	return nil

}

func CanBePaid(ctx context.Context, qtx *db.Queries, order *convert.Order) error {
	payments, err := qtx.GetPaymentDetails(ctx, int32(order.ID))
	if err != nil {
		return fmt.Errorf("failed to get payment details: %w", err)
	}

	for _, payment := range payments {
		if payment.Status.String == "PAID" {
			return fmt.Errorf("order is already paid")
		}
	}

	return nil
}

func UpdateRestriction(ctx context.Context, qtx *db.Queries, order *convert.Order) error {

	// get platform
	dp, err := qtx.GetDeliveryPlatformById(ctx, uint64(order.PlatformID))
	if err != nil {
		return fmt.Errorf("failed to get delivery platform: %w", err)
	}

	if dp.PlatformID.Int32 != 9 {
		return fmt.Errorf("update restriction is applied for non POS orders")
	}

	return nil
}

func TableAvailability(ctx context.Context, qtx *db.Queries, order *convert.Order) error {

	// only applied for dine-in orders
	// and table id should not exist for non dine-in orders
	if order.ShippingMethod.String != "DINE-IN" {
		order.DgposTableID = sql.NullInt32{}
		return nil
	}

	table, err := qtx.GetTableByID(ctx, uint64(order.DgposTableID.Int32))
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return fmt.Errorf("failed to get table: %w", err)
	}

	if table.ID == 0 {
		return fmt.Errorf("table %d does not exist", order.DgposTableID.Int32)
	}

	if table.Status != "AVAILABLE" {
		return posErr.NewIncorrectInputError(
			"Table Unavailable",
			fmt.Sprintf("Table %s is currently unavailable. Please pick another available table to place this order.", table.Name),
		)
	}

	return nil
}

func ItemAvailability(ctx context.Context, qtx *db.Queries, order *convert.Order) error {
	itemIDs := make([]sql.NullInt32, 0, len(order.OrderItems))
	for _, item := range order.OrderItems {
		itemIDs = append(itemIDs, sql.NullInt32{Int32: parseInt(item.ItemID), Valid: true})
	}

	availableItems, err := qtx.GetAvailableOrderItems(ctx, &db.GetAvailableOrderItemsParams{
		ItemIds:            itemIDs,
		DeliveryPlatformID: order.PlatformID,
	})
	if err != nil {
		return fmt.Errorf("failed to get available items: %w", err)
	}

	availableItemIDs := make(map[int32]bool, len(availableItems))
	for _, item := range availableItems {
		availableItemIDs[item.EntityID.Int32] = true
	}

	for _, itemID := range itemIDs {
		if !availableItemIDs[itemID.Int32] {
			return fmt.Errorf("item %d is not available", itemID.Int32)
		}
	}

	return nil
}

func parseInt(dbValue string) int32 {
	value, _ := strconv.Atoi(dbValue)
	return int32(value)
}
