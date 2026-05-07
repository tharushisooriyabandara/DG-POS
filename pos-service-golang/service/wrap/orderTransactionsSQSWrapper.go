package wrap

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	awssqs "github.com/Delivergate-Dev/pos-service-golang/mq/aws-sqs"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/elliotchance/phpserialize"
	"go.uber.org/zap"
)

// orderTransactionsSQSWrapper sends order related transactions to the transactions queue.
// it implements the order interface with embedding the orderService and overriding the necessary methods.
// whenever an order sales or refund transaction is created, it sends a message to the transactions queue.
type orderTransactionsSQSWrapper struct {
	orderService
	db     *db.Queries
	logger *zap.Logger
	bypass bool
}

func NewOrderTransactionsSQSWrapper(bypass bool, conn *sql.DB, logger *zap.Logger, service orderService) orderService {
	return &orderTransactionsSQSWrapper{
		orderService: service,
		db:           db.New(conn),
		logger:       logger,
		bypass:       bypass,
	}
}

func (o *orderTransactionsSQSWrapper) UpdateOrderStatus(ctx context.Context, updateReq *types.UpdateOrderStatusRequest) (*types.GetOrderResponse, error) {
	order, err := o.orderService.UpdateOrderStatus(ctx, updateReq)
	if err != nil {
		return nil, err
	}

	if o.bypass || !o.shouldSentToQueue(order) {
		return order, nil
	}

	if err := o.sendOrderTransactionSqsMessage(ctx, order.ID, updateReq.Requestor.TenantCode); err != nil {
		o.logger.Error("transaction wrapper failed to send order transaction to sqs", zap.Error(err))
	}

	return order, nil
}

func (o *orderTransactionsSQSWrapper) UpdateOrderPayment(ctx context.Context, orderPaymentRequest *types.CreateOrderPaymentRequest) (*types.GetOrderResponse, error) {
	order, err := o.orderService.UpdateOrderPayment(ctx, orderPaymentRequest)
	if err != nil {
		return nil, err
	}

	if o.bypass || !o.shouldSentToQueue(order) {
		return order, nil
	}

	if err := o.sendOrderTransactionSqsMessage(ctx, order.ID, orderPaymentRequest.Requestor.TenantCode); err != nil {
		o.logger.Error("transaction wrapper failed to send order transaction to sqs", zap.Error(err))
	}

	return order, nil
}

func (o *orderTransactionsSQSWrapper) RefundOrder(ctx context.Context, refundOrderRequest *types.RefundOrderRequest) (*types.GetOrderResponse, error) {
	order, err := o.orderService.RefundOrder(ctx, refundOrderRequest)
	if err != nil {
		return order, err
	}

	if o.bypass || !o.shouldSentToQueue(order) {
		return order, nil
	}

	// get latest order refund transaction
	transaction, err := o.db.GetLatestRefundByOrderID(ctx, order.ID)
	if err != nil {
		return order, fmt.Errorf("failed to get order transaction: %w", err)
	}

	// TODO: Fri Jan 23 11:47:43 +0530 2026
	// FIX THIS REPETITION

	content := serializedContent{
		"transaction": serializedContent{
			"transaction": newOrderTransactionsData([]*db.GetOrderTransactionsRow{
				{
					OrderTransaction:    transaction.OrderTransaction,
					RefundID:            sql.NullInt64{Int64: int64(transaction.RefundID), Valid: true},
					Reason:              transaction.Reason,
					RefundTransactionID: sql.NullInt64{Int64: int64(transaction.OrderTransaction.ID), Valid: true},
				},
			}, refundOrderRequest.Requestor.TenantCode),
		},
	}

	phpSerialized, err := phpserialize.Marshal(content, &phpserialize.MarshalOptions{})
	if err != nil {
		return order, fmt.Errorf("failed to serialize customer message: %w", err)
	}

	dataMsg := messageBody{
		Data: data{
			Command: string(phpSerialized),
		},
	}

	dataMsgBody, err := json.Marshal(dataMsg)
	if err != nil {
		return order, fmt.Errorf("failed to marshal data message: %w", err)
	}

	awssqs.Send(ctx, awssqs.Message{
		QueueName:       "transactions",
		MessageBody:     string(dataMsgBody),
		MessageGroupId:  fmt.Sprintf("%d", transaction.OrderTransaction.TypeID),
		DeduplicationID: fmt.Sprintf("%d%d", transaction.OrderTransaction.TypeID, time.Now().UTC().Unix()),
	})

	return order, nil
}

func (o *orderTransactionsSQSWrapper) sendOrderTransactionSqsMessage(ctx context.Context, orderID uint64, masterTenantCode string) error {
	body, err := o.createMessageBody(ctx, orderID, masterTenantCode)
	if err != nil {
		return err
	}

	awssqs.Send(ctx, awssqs.Message{
		QueueName:       "transactions",
		MessageBody:     string(body),
		MessageGroupId:  fmt.Sprintf("%d", orderID),
		DeduplicationID: fmt.Sprintf("%s%d%d", masterTenantCode, orderID, time.Now().UTC().Unix()),
	})

	return nil
}

func (o *orderTransactionsSQSWrapper) createMessageBody(ctx context.Context, orderID uint64, masterTenantCode string) (json.RawMessage, error) {

	transactions, err := o.db.GetOrderTransactions(ctx, orderID)
	if err != nil {
		return nil, fmt.Errorf("failed to get order transactions: %w", err)
	}

	content := serializedContent{
		"transaction": serializedContent{
			"transaction": newOrderTransactionsData(transactions, masterTenantCode),
		},
	}

	phpSerialized, err := phpserialize.Marshal(content, &phpserialize.MarshalOptions{})
	if err != nil {
		return nil, fmt.Errorf("failed to serialize customer message: %w", err)
	}

	dataMsg := messageBody{
		Data: data{
			Command: string(phpSerialized),
		},
	}

	dataMsgBody, err := json.Marshal(dataMsg)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal data message: %w", err)
	}

	return dataMsgBody, nil
}

func newOrderTransactionsData(transactions []*db.GetOrderTransactionsRow, masterTenantCode string) []serializedContent {
	transactionsData := make([]serializedContent, 0, len(transactions))
	for _, transaction := range transactions {
		transactionsData = append(transactionsData, newOrderTransactionData(transaction, masterTenantCode))
	}

	return transactionsData
}

func newOrderTransactionData(transaction *db.GetOrderTransactionsRow, masterTenantCode string) serializedContent {
	t := serializedContent{
		"master_tenant_code": masterTenantCode,
		"id":                 transaction.OrderTransaction.ID,
		"type_id":            transaction.OrderTransaction.TypeID,
		"type":               transaction.OrderTransaction.Type,
		"transaction_type":   transaction.OrderTransaction.TransactionType,
		"transaction_amount": transaction.OrderTransaction.TransactionAmount,
		"transaction_mode":   nullableStr(transaction.OrderTransaction.TransactionMode.String),
		"payment_type":       nullableStr(transaction.OrderTransaction.PaymentType.String),
		"platform":           transaction.OrderTransaction.Platform.String,
		"created_at":         transaction.OrderTransaction.CreatedAt.Time.Format("2006-01-02 15:04:05"),
		"updated_at":         transaction.OrderTransaction.UpdatedAt.Time.Format("2006-01-02 15:04:05"),
	}

	if transaction.OrderTransaction.TransactionType == "REFUND" {
		t["order_refund"] = newOrderRefundData(transaction, masterTenantCode)
	}

	return t
}

func newOrderRefundData(refund *db.GetOrderTransactionsRow, masterTenantCode string) serializedContent {
	return serializedContent{
		"master_tenant_code": masterTenantCode,
		"id":                 refund.RefundID.Int64,
		"type_id":            refund.OrderTransaction.TypeID,
		"type":               refund.OrderTransaction.Type,
		"transaction_id":     refund.RefundTransactionID.Int64,
		"refund_amount":      refund.OrderTransaction.TransactionAmount,
		"refund_mode":        refund.OrderTransaction.TransactionMode.String,
		"reason":             nullableStr(refund.Reason.String),
		"created_at":         refund.OrderTransaction.CreatedAt.Time.Format("2006-01-02 15:04:05"),
		"updated_at":         refund.OrderTransaction.UpdatedAt.Time.Format("2006-01-02 15:04:05"),
	}
}

func (*orderTransactionsSQSWrapper) shouldSentToQueue(order *types.GetOrderResponse) bool {
	orderIsInFinalState := order.Status == "CANCELED" || order.Status == "COMPLETED"
	orderIsPaid := order.PaymentMode != ""

	return orderIsInFinalState && orderIsPaid
}
