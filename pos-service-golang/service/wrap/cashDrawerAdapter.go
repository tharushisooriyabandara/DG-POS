package wrap

import (
	"context"
	"database/sql"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

type CashDrawerAdapter struct {
	cashDrawerService
	orderService
	db *db.Queries
}

func NewCashDrawerAdapter(conn *sql.DB, cashDrawerService cashDrawerService, orderService orderService) *CashDrawerAdapter {
	return &CashDrawerAdapter{
		cashDrawerService: cashDrawerService,
		orderService:      orderService,
		db:                db.New(conn),
	}
}

func (ca *CashDrawerAdapter) CreateOrder(ctx context.Context, orderRequest *types.CreateOrderRequest) (*types.GetOrderResponse, error) {
	order, err := ca.orderService.CreateOrder(ctx, orderRequest)
	if err != nil {
		return nil, err
	}

	ca.recordCashSale(ctx, orderRequest.Requestor, orderRequest.OrderPayments)

	return order, nil
}

func (ca *CashDrawerAdapter) UpdateOrderPayment(ctx context.Context, orderPaymentRequest *types.CreateOrderPaymentRequest) (*types.GetOrderResponse, error) {
	order, err := ca.orderService.UpdateOrderPayment(ctx, orderPaymentRequest)
	if err != nil {
		return nil, err
	}

	ca.recordCashSale(ctx, orderPaymentRequest.Requestor, orderPaymentRequest.Payments)

	return order, nil
}

func (ca *CashDrawerAdapter) recordCashSale(ctx context.Context, requestor types.SessionUser, payments []*types.OrderPayment) {

	totalCashSale := totalCashPayment(payments)
	if totalCashSale <= 0 {
		return
	}

	ca.recordCashMovement(ctx, requestor, "SALE", totalCashSale)
}

func (ca *CashDrawerAdapter) RefundOrder(ctx context.Context, refundOrderRequest *types.RefundOrderRequest) (*types.GetOrderResponse, error) {
	salesData, err := ca.db.GetSalesAndRefundByTypeID(ctx, refundOrderRequest.OrderID)
	if err != nil {
		return nil, err
	}

	cashRefundableBalance := max(salesData.CashSale-salesData.CashRefund, 0)
	refundingAmount := int32(refundOrderRequest.RefundAmount * 100)
	cashRefundAmount := min(cashRefundableBalance, refundingAmount)
	cardRefundAmount := refundingAmount - cashRefundAmount

	order, err := ca.orderService.RefundOrder(ctx, refundOrderRequest)
	if err != nil {
		return order, err
	}

	if refundOrderRequest.RefundMode != "CASH" {
		return order, nil
	}

	switch order.PaymentMode {
	case "CASH":
		ca.recordCashMovement(ctx, refundOrderRequest.Requestor, "REFUND", refundOrderRequest.RefundAmount)

	case "CARD":
		ca.recordCashMovement(ctx, refundOrderRequest.Requestor, "POS_CARD_SALE_CASH_REFUND", refundOrderRequest.RefundAmount)

	case "SPLIT":
		if cashRefundAmount > 0 {
			ca.recordCashMovement(ctx, refundOrderRequest.Requestor, "REFUND", float64(cashRefundAmount)/100)
		}
		if cardRefundAmount > 0 {
			ca.recordCashMovement(ctx, refundOrderRequest.Requestor, "POS_CARD_SALE_CASH_REFUND", float64(cardRefundAmount)/100)
		}
	}

	return order, nil
}

func (ca *CashDrawerAdapter) recordCashMovement(
	ctx context.Context,
	requestor types.SessionUser,
	movementType string,
	amount float64,
) error {
	return ca.cashDrawerService.RecordCashMovement(ctx, types.RecordCashMovementRequest{
		Requestor:    requestor,
		MovementType: movementType,
		Amount:       amount,
	})
}

func totalCashPayment(payments []*types.OrderPayment) float64 {
	var total float64
	for i := range payments {
		if payments[i].PaymentMethod == "CASH" {
			total += payments[i].PayingAmount
		}
	}
	return total
}
