package service

import (
	"context"
	"database/sql"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"go.uber.org/zap"
)

type activityLogService struct {
	logger *zap.Logger
	db     *sql.DB
}

func NewActivityLogService(logger *zap.Logger, db *sql.DB) *activityLogService {
	return &activityLogService{
		logger: logger,
		db:     db,
	}
}

func (a *activityLogService) CreateActivity(ctx context.Context, activityLogReq *types.LogActivityRequest) error {
	queries := db.New(a.db)
	params := NewEvent(
		activityLogReq.Requestor,
		activityLogReq.Event,
		activityLogReq.Subject,
		activityLogReq.SubjectId,
		activityLogReq.Description,
	).toDBParams()
	if err := queries.CreateActivityLog(ctx, &params); err != nil {
		a.logger.Error("Failed to create activity log", zap.Error(err))
		return err
	}
	return nil
}

type Event struct {
	Event       string
	Subject     string
	SubjectId   uint64
	CauserId    uint64
	Description string
	BrandID     int32
	ShopID      uint64
}

func NewEvent(causer types.SessionUser, event string, subject string, subjectId uint64, description string) Event {
	return Event{
		Event:       event,
		Subject:     subject,
		SubjectId:   subjectId,
		CauserId:    causer.ID,
		Description: description,
		BrandID:     causer.BrandID,
		ShopID:      causer.ShopID,
	}
}

func (e Event) toDBParams() db.CreateActivityLogParams {
	return db.CreateActivityLogParams{
		Event:       e.Event,
		LogName:     e.Subject,
		SubjectID:   int64(e.SubjectId),
		CauserID:    int64(e.CauserId),
		Description: sql.NullString{String: e.Description, Valid: e.Description != ""},
		BrandID:     sql.NullInt64{Int64: int64(e.BrandID), Valid: true},
		ShopID:      sql.NullInt64{Int64: int64(e.ShopID), Valid: true},
	}
}
