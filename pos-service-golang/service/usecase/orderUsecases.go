package usecase

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"math"
	"strconv"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
	"github.com/Delivergate-Dev/pos-service-golang/service/generate"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

type Option func(ctx context.Context, queries *db.Queries, order *convert.Order) error

func Apply(ctx context.Context, queries *db.Queries, order *convert.Order, options ...Option) error {
	for _, option := range options {
		if err := option(ctx, queries, order); err != nil {
			return err
		}
	}
	return nil
}

func If(condition bool, operation Option) Option {
	return func(ctx context.Context, queries *db.Queries, order *convert.Order) error {
		if condition {
			return operation(ctx, queries, order)
		}
		return nil
	}
}

func OrderItemModifierString(orderItems []*types.OrderItem) Option {
	return func(ctx context.Context, queries *db.Queries, order *convert.Order) (err error) {
		for i, item := range orderItems {
			for _, modifier := range item.ModifierDetails {
				if modifier.ModifierItem.TaxDetails != nil {
					tax, err := queries.GetTax(ctx, uint64(modifier.ModifierItem.TaxDetails.TaxID))
					if err != nil {
						return err
					}
					taxRate, _ := strconv.ParseFloat(tax.Rate.String, 64)
					modifier.ModifierItem.TaxDetails.TaxCode = tax.Code
					modifier.ModifierItem.TaxDetails.TaxRate = taxRate

					// nested modifiers
					for _, nestedModifier := range modifier.Modifiers {
						if nestedModifier.ModifierItem.TaxDetails != nil {
							tax, err := queries.GetTax(ctx, uint64(nestedModifier.ModifierItem.TaxDetails.TaxID))
							if err != nil {
								return err
							}
							taxRate, _ := strconv.ParseFloat(tax.Rate.String, 64)
							nestedModifier.ModifierItem.TaxDetails.TaxCode = tax.Code
							nestedModifier.ModifierItem.TaxDetails.TaxRate = taxRate
						}
					}
				}
			}

			var str string
			if len(item.ModifierDetails) > 0 {
				str, err = generate.ModifierString(item.ModifierDetails)
				if err != nil {
					return err
				}
			}
			order.OrderItems[i].Modifiers = sql.NullString{String: str, Valid: str != ""}
		}
		return nil
	}
}

// GuestCustomer handles the case when the customer is a guest
// if the customer is a guest, it sets the customer ID and name to the tenant specific guest customer ID and name
func GuestCustomer(ctx context.Context, queries *db.Queries, order *convert.Order) error {
	if order.CustomerID.Int32 != 0 || order.CustomerName.String != "" {
		return nil
	}

	// get tenant guest customer ID and name
	guestCustomer, err := queries.GetGuestCustomer(ctx)
	if err != nil {
		return fmt.Errorf("guest customer not found: %w", err)
	}

	order.CustomerID = sql.NullInt32{Int32: int32(guestCustomer.ID), Valid: true}
	order.CustomerName = sql.NullString{String: fmt.Sprintf("%s %s", guestCustomer.FirstName.String, guestCustomer.LastName.String), Valid: true}

	return nil

}

// OrderCommission handles the case when the order commission needs to be calculated
// it calculates the order commission based on the order amount and the commission percentage
func OrderCommission(ctx context.Context, queries *db.Queries, order *convert.Order) error {

	// get order commission from shop_brand_details
	commissionStr, err := queries.GetCommissionAmount(ctx, &db.GetCommissionAmountParams{
		ShopID:         uint32(order.ShopID),
		WebshopBrandID: 1,
	})
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return fmt.Errorf("failed to get commission amount: %w", err)
	}

	commission, _ := strconv.ParseFloat(commissionStr, 64)

	// calculate order commission and set
	order.OrderCommission.Commission = fmt.Sprintf("%.2f",
		math.Round(float64(order.TotalAmount)*commission*0.01),
	)

	return nil
}

func OrderPayments(ctx context.Context, queries *db.Queries, order *convert.Order) error {
	// order is a pre-paid. No need for a default payment.
	if len(order.OrderPayments) != 0 {
		order.PaymentMethod = order.PaymentMode
		return nil
	}

	const payOnTakeaway = "PAY_ON_TAKEAWAY"
	const payOnCheckout = "PAY_ON_CHECKOUT"
	const payOnDelivery = "PAY_ON_DELIVERY"

	defaultPayment := &db.DgPosPayment{
		ID:                   0,
		OrderID:              0,
		TransactionID:        sql.NullInt64{},
		DateTime:             sql.NullTime{Time: time.Now().UTC(), Valid: true},
		Amount:               strconv.Itoa(int(order.TotalAmount)),
		Cash:                 "0.00",
		Balance:              "0.00",
		CardTransactionToken: sql.NullString{},
		RefundID:             sql.NullString{},
		PaymentMethodID:      sql.NullString{},
		Status:               sql.NullString{String: "UNPAID", Valid: true},
		IsRefund:             false,
		PaymentType:          sql.NullString{},
		CreatedAt:            sql.NullTime{Time: time.Now().UTC(), Valid: true},
		UpdatedAt:            sql.NullTime{Time: time.Now().UTC(), Valid: true},
	}

	order.OrderPayments = []*db.DgPosPayment{defaultPayment}
	switch order.ShippingMethod.String {
	case "DINE-IN":
		order.PaymentMethod = sql.NullString{String: payOnCheckout, Valid: true}

	case "TAKEAWAY":
		order.PaymentMethod = sql.NullString{String: payOnTakeaway, Valid: true}

	case "DELIVERY":
		order.PaymentMethod = sql.NullString{String: payOnDelivery, Valid: true}
	}

	return nil
}

// OrderLocation handles the case when the order receiver is required for delivery orders.
// if the order is a delivery order, it sets the order location to the order receiver
// otherwise
//
//	order.OrderLocation
//
// stays nil
type customerCryptoService interface {
	DecryptCustomer(ctx context.Context, customer *db.Customer) (*db.Customer, error)
}

func OrderLocation(orderReceiverAddressID int32, customerDecryptService customerCryptoService) Option {
	return func(ctx context.Context, queries *db.Queries, order *convert.Order) error {

		// rule does not apply for dine-in and takeaway orders
		if order.ShippingMethod.String == "DINE-IN" || order.ShippingMethod.String == "TAKEAWAY" {
			return nil
		}

		// order receiver is required for delivery orders
		if order.ShippingMethod.String == "DELIVERY" && orderReceiverAddressID == 0 {
			return errors.New("order receiver is required for delivery orders")
		}

		orderReceiver, err := queries.GetOrderReceiver(ctx, &db.GetOrderReceiverParams{
			ID:         uint64(orderReceiverAddressID),
			CustomerID: order.CustomerID.Int32,
		})
		if err != nil {
			if errors.Is(err, sql.ErrNoRows) {
				return errors.New("customer address does not exist")
			}
			return fmt.Errorf("failed to get customer address: %w", err)
		}
		decryptedCustomer, err := customerDecryptService.DecryptCustomer(ctx, &orderReceiver.Customer)
		if err != nil {
			return fmt.Errorf("failed to decrypt customer: %w", err)
		}
		orderReceiver.Customer = *decryptedCustomer

		order.OrderLocation = convert.OrderLocation(orderReceiver)
		order.DeliveryLocationID = sql.NullInt32{Int32: int32(orderReceiver.DeliveryLocation.ID), Valid: true}

		return nil
	}
}

func OrderTimestamps(orderStatus string) Option {
	return func(_ context.Context, _ *db.Queries, order *convert.Order) error {
		switch {
		case orderStatus == "QUEUE":
			order.OrderTimestamps.Queue = sql.NullTime{Time: time.Now().UTC(), Valid: true}

		case orderStatus == "PREPARING":
			order.OrderTimestamps.Preparing = sql.NullTime{Time: time.Now().UTC(), Valid: true}

		case orderStatus == "READY":
			order.OrderTimestamps.Ready = sql.NullTime{Time: time.Now().UTC(), Valid: true}

		case orderStatus == "SERVED":
			order.OrderTimestamps.Served = sql.NullTime{Time: time.Now().UTC(), Valid: true}

		case orderStatus == "DELIVERED" && order.ShippingMethod.String == "DELIVERY":
			order.OrderTimestamps.Delivered = sql.NullTime{Time: time.Now().UTC(), Valid: true}

		case orderStatus == "COMPLETED" && (order.ShippingMethod.String == "TAKEAWAY" || order.ShippingMethod.String == "DELIVERY"):
			order.OrderTimestamps.Completed = sql.NullTime{Time: time.Now().UTC(), Valid: true}

		case
			orderStatus == "DELIVERED" && order.ShippingMethod.String != "DELIVERY":
			return fmt.Errorf("order status %s is not applicable for %s order type", orderStatus, order.ShippingMethod.String)
		}

		order.OrderTimestamps.UpdatedAt = sql.NullTime{Time: time.Now().UTC(), Valid: true}
		return nil
	}
}
