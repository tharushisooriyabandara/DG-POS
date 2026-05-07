package service

import (
	"context"
	"database/sql"
	"errors"
	"math"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

type temporaryPaymentService struct {
	db *db.Queries
}

func NewTemporaryPaymentService(conn *sql.DB) *temporaryPaymentService {
	return &temporaryPaymentService{
		db: db.New(conn),
	}
}

func (s *temporaryPaymentService) CreateDgPosTmpPayment(ctx context.Context, payment *types.DgPosTmpPaymentRequest) error {
	paymentAmount := int32(math.Round(payment.PaymentAmount * 100))
	return s.db.InsertDgPosTmpPayment(ctx, &db.InsertDgPosTmpPaymentParams{
		TypeID:        payment.TypeId,
		Type:          payment.Type,
		PaymentMode:   payment.PaymentMode,
		PaymentAmount: paymentAmount,
		TransactionID: sql.NullString{String: payment.TransactionID, Valid: payment.TransactionID != ""},
	})
}

func (s *temporaryPaymentService) GetDgPosTmpPayment(ctx context.Context, orderID string) ([]*types.DgPosTmpPaymentResponse, error) {
	payments, err := s.db.GetDgPosTmpPayments(ctx, orderID)
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return nil, err
	}
	paymentResponses := make([]*types.DgPosTmpPaymentResponse, len(payments))
	for i, payment := range payments {
		paymentResponses[i] = &types.DgPosTmpPaymentResponse{
			TypeID:        payment.TypeID,
			Type:          payment.Type,
			PaymentMode:   payment.PaymentMode,
			PaymentAmount: float64(payment.PaymentAmount) / 100,
			TransactionID: payment.TransactionID.String,
		}
	}
	return paymentResponses, nil
}
