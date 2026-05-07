package convert

import (
	"math"
	"strings"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

func ToCashDrawerSessionsResp(sessions []*db.GetCashDrawerSessionsRow) []*types.GetCashDrawerSessionsResp {
	sessionsResp := make([]*types.GetCashDrawerSessionsResp, len(sessions))
	for i, s := range sessions {
		sessionsResp[i] = toCashDrawerSessionResp(s)
	}
	return sessionsResp
}

func toCashDrawerSessionResp(session *db.GetCashDrawerSessionsRow) *types.GetCashDrawerSessionsResp {
	return &types.GetCashDrawerSessionsResp{
		ID:                     session.ID,
		CashDrawerID:           session.CashDrawerID,
		SessionStartedUserID:   session.SessionStartedUserID,
		SessionStartedUserName: session.SessionStartedUserName,
		SessionEndedUserID:     session.SessionEndedUserID.Int64,
		SessionEndedUserName:   session.SessionEndedUserName,
		OpenedAt:               session.OpenedAt,
		ClosedAt:               session.ClosedAt.Time,
		OpeningBalance:         float64(session.OpeningBalance) / 100.0,
		ClosingBalanceCounted:  float64(session.ClosingBalanceCounted.Int32) / 100.0,
		ClosingBalanceExpected: float64(session.ClosingBalanceExpected.Int32) / 100.0,
		Difference:             float64(session.Difference.Int32) / 100.0,
		TotalInAmount:          float64(session.TotalInAmount.Int32) / 100.0,
		TotalOutAmount:         math.Abs(float64(session.TotalOutAmount.Int32) / 100.0),
		TotalSalesAmount:       float64(session.TotalSalesAmount.Int32) / 100.0,
		TotalOtherSalesAmount:  float64(session.TotalOtherSalesAmount.Int32) / 100.0,
		TotalRefundAmount:      math.Abs(float64(session.TotalRefundAmount.Int32) / 100.0),
		Status:                 session.Status,
		CreatedAt:              session.CreatedAt.Time,
		UpdatedAt:              session.UpdatedAt.Time,
	}
}

func ToCashDrawerTransactionHistoryResp(transactions []*db.GetCashDrawerTransactionHistoryRow) []*types.GetCashDrawerTransactionHistoryResp {
	transactionsResp := make([]*types.GetCashDrawerTransactionHistoryResp, len(transactions))
	for i, t := range transactions {
		transactionsResp[i] = toCashDrawerTransactionHistoryResp(t)
	}
	return transactionsResp
}

func toCashDrawerTransactionHistoryResp(transaction *db.GetCashDrawerTransactionHistoryRow) *types.GetCashDrawerTransactionHistoryResp {
	return &types.GetCashDrawerTransactionHistoryResp{
		CashDrawerID:        transaction.CashDrawerID,
		CashDrawerSessionID: transaction.CashDrawerSessionID,
		CashMovementID:      transaction.CashMovementID,
		CreatedAt:           transaction.CreatedAt.Time,
		MovementType:        transaction.MovementType,
		Note:                transaction.Note.String,
		Amount:              math.Abs(float64(transaction.Amount) / 100.0),
		UserName:            strings.Join([]string{transaction.UserName.String, transaction.UserLastName.String}, " "),
	}
}
