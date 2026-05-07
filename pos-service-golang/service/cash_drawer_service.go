package service

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"math"
	"strings"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	posErr "github.com/Delivergate-Dev/pos-service-golang/errors"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"go.uber.org/zap"
)

var (
	ErrNoActiveCashDrawerSession = errors.New("no active cash drawer session found")
	ErrInsufficientBalance       = errors.New("insufficient balance")
)

type decryptor interface {
	DecryptUserByIDRow(ctx context.Context, user *db.GetUserByIDRow) (*db.GetUserByIDRow, error)
}

type CashDrawerService struct {
	db     *db.Queries
	logger *zap.Logger
	crypto decryptor
}

func NewCashDrawerService(logger *zap.Logger, conn *sql.DB, crypto decryptor) *CashDrawerService {
	return &CashDrawerService{
		logger: logger,
		db:     db.New(conn),
		crypto: crypto,
	}
}

func (s *CashDrawerService) GetActiveCashDrawerSessionInfo(ctx context.Context, req types.GetActiveCashDrawerSessionInfoRequest) (*types.GetActiveCashDrawerSessionInfoResp, error) {

	activeSession, err := s.db.GetOpenCashDrawerSession(ctx, req.Requestor.ShopID)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, ErrNoActiveCashDrawerSession
		}
		return nil, fmt.Errorf("failed to get open cash drawer session: %w", err)
	}

	sessionSummary, err := s.db.GetCashDrawerSessionSummary(ctx, activeSession.ID)
	if err != nil {
		return nil, fmt.Errorf("failed to get cash drawer session expected balance: %w", err)
	}

	// decrypt user names
	decryptedUser, err := s.crypto.DecryptUserByIDRow(ctx, &db.GetUserByIDRow{
		Name:          activeSession.SessionStartedUserName,
		KeyIDName:     activeSession.SessionStartedUserKeyIDName,
		LastName:      activeSession.SessionStartedUserLastName,
		KeyIDLastName: activeSession.SessionStartedUserKeyIDLastName,
	})
	if err != nil {
		return nil, fmt.Errorf("failed to decrypt user: %w", err)
	}

	incompleteOrders, err := s.db.GetIncompleteOrders(ctx, &db.GetIncompleteOrdersParams{
		FromCreatedAt: sql.NullTime{Time: activeSession.OpenedAt, Valid: true},
		ToCreatedAt:   sql.NullTime{Time: time.Now().UTC(), Valid: true},
		ShopID:        int32(req.Requestor.ShopID),
	})
	if err != nil {
		return nil, fmt.Errorf("failed to get incomplete orders: %w", err)
	}
	orderDisplayIDs := make([]string, 0, len(incompleteOrders))
	for _, order := range incompleteOrders {
		orderDisplayIDs = append(orderDisplayIDs, order.DisplayOrderID.String)
	}

	resp := &types.GetActiveCashDrawerSessionInfoResp{
		ID:                            activeSession.ID,
		CashDrawerID:                  activeSession.CashDrawerID,
		SessionStartedUserID:          activeSession.SessionStartedUserID,
		SessionStartedUserName:        strings.Join([]string{decryptedUser.Name, decryptedUser.LastName.String}, " "),
		OpenedAt:                      activeSession.OpenedAt,
		OpeningBalance:                float64(activeSession.OpeningBalance) / 100.0,
		ClosingBalanceExpected:        (float64(sessionSummary.ExpectedBalance) / 100.0),
		TotalInAmount:                 float64(sessionSummary.TotalInAmount) / 100.0,
		TotalOutAmount:                math.Abs((float64(sessionSummary.TotalOutAmount) / 100.0)),
		TotalOtherSalesAmount:         float64(sessionSummary.TotalOtherSalesAmount) / 100.0,
		TotalRefundAmount:             math.Abs(float64(sessionSummary.TotalRefundAmount) / 100.00),
		TotalSalesAmount:              float64(sessionSummary.TotalSalesAmount) / 100.0,
		Status:                        activeSession.Status,
		TotalCashSaleCashRefundAmount: math.Abs(float64(sessionSummary.TotalCashSaleCashRefundAmount) / 100.0),
		TotalCardSaleCashRefundAmount: math.Abs(float64(sessionSummary.TotalCardSaleCashRefundAmount+sessionSummary.TotalPosCardSaleCashRefundAmount) / 100.0),
		TotalOtherRefundAmount:        math.Abs(float64(sessionSummary.TotalOtherRefundAmount) / 100.0),
		CreatedAt:                     activeSession.CreatedAt.Time,
		UpdatedAt:                     activeSession.UpdatedAt.Time,
		IncompleteOrders: types.ActiveSessionIncompleteOrdersResponse{
			Count:  len(incompleteOrders),
			Orders: orderDisplayIDs,
		},
	}

	if req.IsForXReport {
		return s.respondForXReport(ctx, resp, req.Requestor.ShopID, activeSession.OpenedAt, sessionSummary)
	}

	return resp, nil
}

func (s *CashDrawerService) respondForXReport(ctx context.Context, resp *types.GetActiveCashDrawerSessionInfoResp, shopID uint64, openedTime time.Time, sessionSummary *db.GetCashDrawerSessionSummaryRow) (*types.GetActiveCashDrawerSessionInfoResp, error) {

	completedPosOrderIdsWithinSession, err := s.db.GetCashPaidPosOrderIdsWithinTimeRange(ctx, &db.GetCashPaidPosOrderIdsWithinTimeRangeParams{
		FromCreatedAt: sql.NullTime{Time: openedTime, Valid: true},
		ToCreatedAt:   sql.NullTime{Time: time.Now().UTC(), Valid: true},
		ShopID:        int32(shopID),
	})
	if err != nil {
		return nil, fmt.Errorf("failed to get cash paid pos orders within time range: %w", err)
	}

	var _netCashSalePos, _cardSaleCashRefundPos, _cashSaleCashrefundPos int32
	for _, orderId := range completedPosOrderIdsWithinSession {
		transactions, err := s.db.GetCashTransactionsByOrderID(ctx, orderId)
		if err != nil {
			return nil, fmt.Errorf("failed to get cash transaction by order id: %w", err)
		}

		var salesAmount, cardSaleCashRefund, cashSaleCashRefund int32

		for _, transaction := range transactions {
			if transaction.TransactionType == "SALE" {
				salesAmount += transaction.TransactionAmount
				continue
			}

			if transaction.TransactionType == "REFUND" {
				refund := transaction.TransactionAmount

				// amount we can cover from current sales
				covered := min(refund, salesAmount)

				// reduce sales by covered amount
				salesAmount -= covered

				// this part is paid from cash sales
				cashSaleCashRefund += covered

				// remaining goes to card refund
				overflow := refund - covered
				cardSaleCashRefund += overflow
			}
		}

		_netCashSalePos += salesAmount
		_cardSaleCashRefundPos += cardSaleCashRefund
		_cashSaleCashrefundPos += cashSaleCashRefund
	}

	netCashSalePos := float64(_netCashSalePos) / 100.0
	cashSaleCashRefundPOS := float64(_cashSaleCashrefundPos) / 100.0
	cardSaleCashRefundPOS := float64(_cardSaleCashRefundPos) / 100.0

	cardSaleCashRefund := cardSaleCashRefundPOS + math.Abs((float64(sessionSummary.TotalCardSaleCashRefundAmount) / 100.0))

	totalCredit := netCashSalePos + resp.TotalOtherSalesAmount + resp.TotalInAmount
	totalDebit := resp.TotalOutAmount + cardSaleCashRefund

	resp.ClosingBalanceExpected = (resp.OpeningBalance + totalCredit) - totalDebit
	resp.TotalSalesAmount = netCashSalePos
	resp.TotalCashSaleCashRefundAmount = cashSaleCashRefundPOS
	resp.TotalCardSaleCashRefundAmount = cardSaleCashRefund
	resp.TotalRefundAmount = cardSaleCashRefund + cashSaleCashRefundPOS + resp.TotalOtherRefundAmount

	return resp, nil
}

func (s *CashDrawerService) GetCashDrawerSession(ctx context.Context, sessionID uint64) (*types.GetCashDrawerSessionsResp, error) {
	session, err := s.db.GetCashDrawerSessionByID(ctx, sessionID)
	if err != nil {
		return nil, fmt.Errorf("failed to get cash drawer session: %w", err)
	}

	decryptedStartedUser, err := s.crypto.DecryptUserByIDRow(ctx, &db.GetUserByIDRow{
		Name:          session.SessionStartedUserName,
		KeyIDName:     session.SessionStartedUserKeyIDName,
		LastName:      session.SessionStartedUserLastName,
		KeyIDLastName: session.SessionStartedUserKeyIDLastName,
	})
	if err != nil {
		return nil, fmt.Errorf("failed to decrypt user: %w", err)
	}

	decryptedEndedUser, err := s.crypto.DecryptUserByIDRow(ctx, &db.GetUserByIDRow{
		Name:          session.SessionEndedUserName,
		KeyIDName:     session.SessionEndedUserKeyIDName,
		LastName:      session.SessionEndedUserLastName,
		KeyIDLastName: session.SessionEndedUserKeyIDLastName,
	})
	if err != nil {
		return nil, fmt.Errorf("failed to decrypt user: %w", err)
	}

	reps := &types.GetCashDrawerSessionsResp{
		ID:                     session.ID,
		CashDrawerID:           session.CashDrawerID,
		SessionStartedUserID:   session.SessionStartedUserID,
		SessionStartedUserName: strings.Join([]string{decryptedStartedUser.Name, decryptedStartedUser.LastName.String}, " "),
		SessionEndedUserID:     session.SessionEndedUserID.Int64,
		SessionEndedUserName:   strings.Join([]string{decryptedEndedUser.Name, decryptedEndedUser.LastName.String}, " "),
		OpenedAt:               session.OpenedAt,
		OpeningBalance:         float64(session.OpeningBalance) / 100.0,
		ClosedAt:               session.ClosedAt.Time,
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

	summary, err := s.db.GetCashDrawerSessionSummary(ctx, sessionID)
	if err != nil {
		return nil, fmt.Errorf("failed to get cash drawer session summary: %w", err)
	}

	reps.TotalCashSaleCashRefundAmount = math.Abs(float64(summary.TotalCashSaleCashRefundAmount) / 100.0)
	reps.TotalCardSaleCashRefundAmount = math.Abs(float64(summary.TotalCardSaleCashRefundAmount+summary.TotalPosCardSaleCashRefundAmount) / 100.0)
	reps.TotalOtherRefundAmount = math.Abs(float64(summary.TotalOtherRefundAmount) / 100.0)

	return reps, nil
}

func (s *CashDrawerService) GetCashDrawerSessions(ctx context.Context, req types.GetCashDrawerSessionsRequest) ([]*types.GetCashDrawerSessionsResp, error) {

	// already validated, so ignoring the error
	fromDate, _ := time.Parse(time.DateTime, req.From)
	toDate, _ := time.Parse(time.DateTime, req.To)
	toDate = toDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)

	if req.To == "" {
		toDate = fromDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)
	}

	sessions, err := s.db.GetCashDrawerSessions(ctx, &db.GetCashDrawerSessionsParams{
		FromDate: sql.NullTime{Time: fromDate, Valid: true},
		ToDate:   sql.NullTime{Time: toDate, Valid: true},
		OutletID: req.Requestor.ShopID,
	})
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return nil, fmt.Errorf("couldn't get cash drawer sessions : %w", err)
	}

	// decrypt user names
	for _, session := range sessions {
		decryptedStartedUser, err := s.crypto.DecryptUserByIDRow(ctx, &db.GetUserByIDRow{
			Name:          session.SessionStartedUserName,
			KeyIDName:     session.SessionStartedUserKeyIDName,
			LastName:      session.SessionStartedUserLastName,
			KeyIDLastName: session.SessionStartedUserKeyIDLastName,
		})
		if err != nil {
			return nil, fmt.Errorf("failed to decrypt user: %w", err)
		}
		session.SessionStartedUserName = strings.Join([]string{decryptedStartedUser.Name, decryptedStartedUser.LastName.String}, " ")

		decryptedEndedUser, err := s.crypto.DecryptUserByIDRow(ctx, &db.GetUserByIDRow{
			Name:          session.SessionEndedUserName,
			KeyIDName:     session.SessionEndedUserKeyIDName,
			LastName:      session.SessionEndedUserLastName,
			KeyIDLastName: session.SessionEndedUserKeyIDLastName,
		})
		if err != nil {
			return nil, fmt.Errorf("failed to decrypt user: %w", err)
		}
		session.SessionEndedUserName = strings.Join([]string{decryptedEndedUser.Name, decryptedEndedUser.LastName.String}, " ")
	}

	resp := convert.ToCashDrawerSessionsResp(sessions)
	for _, r := range resp {
		if summary, err := s.db.GetCashDrawerSessionSummary(ctx, r.ID); err == nil {
			r.TotalCashSaleCashRefundAmount = math.Abs(float64(summary.TotalCashSaleCashRefundAmount) / 100.0)
			r.TotalCardSaleCashRefundAmount = math.Abs(float64(summary.TotalCardSaleCashRefundAmount+summary.TotalPosCardSaleCashRefundAmount) / 100.0)
			r.TotalOtherRefundAmount = math.Abs(float64(summary.TotalOtherRefundAmount) / 100.0)
		}
	}

	return resp, nil
}

func (s *CashDrawerService) GetCashDrawerTransactionHistory(ctx context.Context, req types.GetCashDrawerTransactionHistoryRequest) ([]*types.GetCashDrawerTransactionHistoryResp, error) {

	// already validated, so ignoring the error
	fromDate, _ := time.Parse(time.DateTime, req.From)
	toDate, _ := time.Parse(time.DateTime, req.To)
	toDate = toDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)

	if req.To == "" {
		toDate = fromDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)
	}

	transactions, err := s.db.GetCashDrawerTransactionHistory(ctx, &db.GetCashDrawerTransactionHistoryParams{
		OutletID: req.Requestor.ShopID,
		FromDate: sql.NullTime{Time: fromDate, Valid: true},
		ToDate:   sql.NullTime{Time: toDate, Valid: true},
	})
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return nil, fmt.Errorf("couldn't get cash drawer transaction history : %w", err)
	}

	// decrypt user names
	for _, transaction := range transactions {
		decryptedUser, err := s.crypto.DecryptUserByIDRow(ctx, &db.GetUserByIDRow{
			Name:          transaction.UserName.String,
			KeyIDName:     transaction.UserKeyIDName.Int32,
			LastName:      transaction.UserLastName,
			KeyIDLastName: transaction.UserKeyIDLastName.Int32,
		})
		if err != nil {
			return nil, fmt.Errorf("failed to decrypt user: %w", err)
		}
		transaction.UserName = sql.NullString{String: decryptedUser.Name, Valid: true}
		transaction.UserLastName = sql.NullString{String: decryptedUser.LastName.String, Valid: true}
	}

	return convert.ToCashDrawerTransactionHistoryResp(transactions), nil
}

func (s *CashDrawerService) CreateCashDrawer(ctx context.Context, req types.CreateCashDrawerRequest) error {

	// check if cash drawer already exists
	cashDrawer, err := s.db.GetCashDrawerByOutletID(ctx, req.Requestor.ShopID)
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return fmt.Errorf("failed to get cash drawer: %w", err)
	}

	if cashDrawer.ID != 0 {
		return fmt.Errorf("cash drawer for this outlet already exists")
	}

	if err := s.db.CreateCashDrawer(ctx, &db.CreateCashDrawerParams{
		OutletID: req.Requestor.ShopID,
		IsActive: true,
	}); err != nil {
		return fmt.Errorf("failed to create cash drawer: %w", err)
	}

	return nil
}

func (s *CashDrawerService) OpenCashDrawerSession(ctx context.Context, req types.OpenCashDrawerSessionRequest) error {

	// check if open session exists
	if openSession, err := s.db.GetOpenCashDrawerSession(ctx, req.Requestor.ShopID); err == nil || openSession.Status == "OPEN" {
		return fmt.Errorf("an open cash drawer session already exists. Please close it before opening a new one")
	}

	cashDrawer, err := s.db.GetCashDrawerByOutletID(ctx, req.Requestor.ShopID)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return fmt.Errorf("cash drawer for requestor's outlet does not exist")
		}
		return fmt.Errorf("failed to fetch cash drawer for outlet : %w", err)
	}

	if err := s.db.CreateCashDrawerSession(ctx, &db.CreateCashDrawerSessionParams{
		CashDrawerID:         cashDrawer.ID,
		OpenedAt:             time.Now().UTC(),
		SessionStartedUserID: req.Requestor.ID,
		OpeningBalance:       convertToCents(req.OpeningBalance),
	}); err != nil {
		return fmt.Errorf("failed to open cash drawer session: %w", err)
	}

	return nil
}

func (s *CashDrawerService) CloseCashDrawerSession(ctx context.Context, req types.CloseCashDrawerSessionRequest) error {

	drawerSession, err := s.db.GetOpenCashDrawerSession(ctx, req.Requestor.ShopID)
	if err != nil {
		return fmt.Errorf("failed to get open cash drawer session: %w", err)
	}

	incompleteOrders, err := s.db.GetIncompleteOrders(ctx, &db.GetIncompleteOrdersParams{
		FromCreatedAt: sql.NullTime{Time: drawerSession.OpenedAt, Valid: true},
		ToCreatedAt:   sql.NullTime{Time: time.Now().UTC(), Valid: true},
		ShopID:        int32(req.Requestor.ShopID),
	})
	if err != nil {
		return fmt.Errorf("failed to get incomplete orders: %w", err)
	}

	if len(incompleteOrders) > 0 {
		return CannotEndSessionError{incompleteOrders: incompleteOrders}
	}

	if err := s.db.UpdateClosingBalanceCounted(ctx, &db.UpdateClosingBalanceCountedParams{
		ClosingBalanceCounted: sql.NullInt32{Int32: convertToCents(req.ClosingBalanceCounted), Valid: true},
		ID:                    drawerSession.ID,
	}); err != nil {
		return fmt.Errorf("failed to update closing balance counted: %w", err)
	}

	summary, err := s.db.GetCashDrawerSessionSummary(ctx, drawerSession.ID)
	if err != nil {
		return fmt.Errorf("failed to get cash drawer session summary: %w", err)
	}

	if err := s.db.CloseCashDrawerSession(ctx, &db.CloseCashDrawerSessionParams{
		SessionEndedUserID:     sql.NullInt64{Int64: int64(req.Requestor.ID), Valid: true},
		ClosingBalanceExpected: sql.NullInt32{Int32: summary.ExpectedBalance, Valid: true},
		Difference:             sql.NullInt32{Int32: summary.Difference, Valid: true},
		TotalInAmount:          sql.NullInt32{Int32: summary.TotalInAmount, Valid: true},
		TotalOutAmount:         sql.NullInt32{Int32: summary.TotalOutAmount, Valid: true},
		TotalSalesAmount:       sql.NullInt32{Int32: summary.TotalSalesAmount, Valid: true},
		TotalOtherSalesAmount:  sql.NullInt32{Int32: summary.TotalOtherSalesAmount, Valid: true},
		TotalRefundAmount:      sql.NullInt32{Int32: summary.TotalRefundAmount, Valid: true},
		ID:                     drawerSession.ID,
	}); err != nil {
		return fmt.Errorf("failed to close cash drawer session: %w", err)
	}

	return nil
}

func (s *CashDrawerService) RecordCashMovement(ctx context.Context, req types.RecordCashMovementRequest) error {

	drawerSession, err := s.db.GetOpenCashDrawerSession(ctx, req.Requestor.ShopID)
	if err != nil {
		return fmt.Errorf("failed to get open cash drawer session: %w", err)
	}

	params := &db.UpsertCashMovementParams{
		ID:                  0,
		CashDrawerSessionID: drawerSession.ID,
		UserID:              req.Requestor.ID,
		MovementType:        req.MovementType,
		Note:                sql.NullString{String: req.Note, Valid: req.Note != ""},
	}

	switch req.MovementType {
	case "PAY_IN":
		params.Amount = convertToCents(req.Amount) // positive amount for cash in
		if err := s.db.UpsertCashMovement(ctx, params); err != nil {
			return fmt.Errorf("failed to record cash movement: %w", err)
		}

	case "PAY_OUT":
		if err := s.canPayOut(ctx, drawerSession.ID, req.Amount); err != nil {
			return posErr.NewIncorrectInputError(
				"Insufficient balance",
				"The Requested amount exceeds the available balance.",
			)
		}

		params.Amount = -convertToCents(req.Amount) // negative amount for cash out
		if err := s.db.UpsertCashMovement(ctx, params); err != nil {
			return fmt.Errorf("failed to record cash movement: %w", err)
		}

	case "SALE", "OTHER_SALES":
		if err := s.createOrUpdateMovement(ctx, drawerSession.ID, req.MovementType, req.Amount); err != nil {
			return fmt.Errorf("failed to create or update other sales: %w", err)
		}

	case "REFUND", "POS_CARD_SALE_CASH_REFUND", "CARD_SALE_CASH_REFUND", "OTHER_REFUND":
		if err := s.createOrUpdateMovement(ctx, drawerSession.ID, req.MovementType, -req.Amount); err != nil {
			return fmt.Errorf("failed to create or update other sales: %w", err)
		}
	}

	return nil
}

func (s *CashDrawerService) createOrUpdateMovement(ctx context.Context, sessionID uint64, salesType string, amount float64) error {
	// check if other sales exists
	movement, err := s.db.GetCashMovement(ctx, &db.GetCashMovementParams{
		CashDrawerSessionID: sessionID,
		MovementType:        salesType,
	})
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return fmt.Errorf("failed to get cash movement: %w", err)
	}

	var note string
	switch salesType {
	case "SALE":
		note = "Total amount of POS cash sales for this session"
	case "OTHER_SALES":
		note = "Total amount of NON-POS cash sales for this session"
	case "REFUND":
		note = "Total amount of POS cash refunds for this session"
	case "OTHER_REFUND":
		note = "Total amount of NON-POS cash refunds for this session"
	}

	if err := s.db.UpsertCashMovement(ctx, &db.UpsertCashMovementParams{
		ID:                  movement.ID,
		CashDrawerSessionID: sessionID,
		MovementType:        salesType,
		Note:                sql.NullString{String: note, Valid: true},
		Amount:              convertToCents(amount),
	}); err != nil {
		return fmt.Errorf("failed to record cash movement: %w", err)
	}

	return nil
}

func (s *CashDrawerService) canPayOut(ctx context.Context, sessionID uint64, amount float64) error {
	summary, err := s.db.GetCashDrawerSessionSummary(ctx, sessionID)
	if err != nil {
		return fmt.Errorf("failed to get cash drawer session expected balance: %w", err)
	}

	currentBalance := (float64(summary.ExpectedBalance) / 100.0)
	if currentBalance < amount {
		return fmt.Errorf("current balance: %.2f, requested amount: %.2f", currentBalance, amount)
	}

	return nil
}

func convertToCents(n float64) int32 {
	return int32(n * 100)
}

type CannotEndSessionError struct {
	incompleteOrders []*db.Order
}

func (incompleteOrderError CannotEndSessionError) Error() string {
	displayOrderIDs := make([]string, 0, len(incompleteOrderError.incompleteOrders))
	for _, order := range incompleteOrderError.incompleteOrders {
		displayOrderIDs = append(displayOrderIDs, order.DisplayOrderID.String)
	}

	var list string
	if len(displayOrderIDs) < 3 {
		list = fmt.Sprintf("(%s)", strings.Join(displayOrderIDs, ", "))
	} else {
		list = fmt.Sprintf("(%s, ...)", strings.Join(displayOrderIDs[:3], ", "))
	}

	count := len(incompleteOrderError.incompleteOrders)
	errrmsg := "Please complete or cancel all pending orders before ending the shift."

	if count == 1 {
		return fmt.Sprintf("There is 1 incomplete order %s. %s", list, errrmsg)
	}

	return fmt.Sprintf("There are %d incomplete orders %s. %s", count, list, errrmsg)
}
