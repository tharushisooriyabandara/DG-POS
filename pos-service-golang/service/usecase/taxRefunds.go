package usecase

import (
	"context"
	"fmt"
	"math"
	"strconv"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
)

func TaxRefund(ctx context.Context, queries *db.Queries, order *db.Order, ra int32) ([]*db.OrderTax, error) {
	// get order sales taxes
	orderSalesTaxes, err := queries.GetOrderSalesTaxes(ctx, int32(order.ID))
	if err != nil {
		return nil, fmt.Errorf("tax refund usecase failed to get order sales taxes: %w", err)
	}

	orderTotal := float64(order.TotalAmount) / 100.00
	refundAmount := float64(ra) / 100.00

	// for each sales tax, calculate refund tax record
	orderRefundTaxes := make([]*db.OrderTax, 0, len(orderSalesTaxes))
	for _, salesTax := range orderSalesTaxes {
		taxRate, _ := strconv.ParseFloat(salesTax.TaxRate, 64)
		taxableAmount := float64(salesTax.TaxableAmount) / 100.00

		refundingTaxableAmount := (taxableAmount * refundAmount) / orderTotal
		refundingTaxAmount := (refundingTaxableAmount * taxRate) / (100 + taxRate)

		fmt.Printf("refundingTaxAmount: %v\n", refundingTaxAmount)

		orderRefundTaxes = append(orderRefundTaxes, &db.OrderTax{
			TaxRate:       salesTax.TaxRate,
			TaxCode:       salesTax.TaxCode,
			TaxAmount:     int32(math.Round(refundingTaxAmount * 100)),
			TaxableAmount: int32(math.Round(refundingTaxableAmount * 100)),
			Type:          "REFUND",
		})
	}

	return orderRefundTaxes, nil
}
