package service

import (
	"context"
	"database/sql"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

type IncomingOrderService struct {
	queries *db.Queries
}

func NewIncomingOrderService(conn *sql.DB) *IncomingOrderService {
	return &IncomingOrderService{
		queries: db.New(conn),
	}
}

func (s *IncomingOrderService) UpdateOrderUserID(ctx context.Context, updateReq *types.UpdateOrderUserIDRequest) error {
	return s.queries.UpdateOrderUserID(ctx, &db.UpdateOrderUserIDParams{
		UserID: sql.NullInt32{Int32: updateReq.UserID, Valid: true},
		ID:     updateReq.OrderID,
	})
}
