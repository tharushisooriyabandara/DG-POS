package wrap

import (
	"context"
	"fmt"

	"github.com/Delivergate-Dev/pos-service-golang/types"
)

type orderActivityLogAdapter struct {
	orderService
	activityLogService
}

func NewOrderActivityLogger(activityLog activityLogService, service orderService) orderService {
	return &orderActivityLogAdapter{
		activityLogService: activityLog,
		orderService:       service,
	}
}

func (a *orderActivityLogAdapter) CreateOrder(ctx context.Context, orderRequest *types.CreateOrderRequest) (*types.GetOrderResponse, error) {
	order, err := a.orderService.CreateOrder(ctx, orderRequest)
	if err != nil {
		return nil, err
	}

	a.activityLogService.CreateActivity(ctx, &types.LogActivityRequest{
		Requestor:   orderRequest.Requestor,
		Event:       "create",
		Subject:     "order",
		SubjectId:   order.ID,
		Description: fmt.Sprintf("Created order %s", order.DisplayOrderID),
	})

	return order, nil
}

func (a *orderActivityLogAdapter) UpdateOrder(ctx context.Context, orderID uint64, orderRequest *types.UpdateOrderRequest) (*types.GetOrderResponse, error) {
	order, err := a.orderService.UpdateOrder(ctx, orderID, orderRequest)
	if err != nil {
		return nil, err
	}

	a.activityLogService.CreateActivity(ctx, &types.LogActivityRequest{
		Requestor:   orderRequest.Requestor,
		Event:       "update",
		Subject:     "order",
		SubjectId:   order.ID,
		Description: fmt.Sprintf("Updated order %s", order.DisplayOrderID),
	})

	return order, nil
}

func (a *orderActivityLogAdapter) UpdateOrderStatus(ctx context.Context, updateReq *types.UpdateOrderStatusRequest) (*types.GetOrderResponse, error) {
	order, err := a.orderService.UpdateOrderStatus(ctx, updateReq)
	if err != nil {
		return nil, err
	}

	a.activityLogService.CreateActivity(ctx, &types.LogActivityRequest{
		Requestor:   updateReq.Requestor,
		Event:       "update",
		Subject:     "order",
		SubjectId:   order.ID,
		Description: fmt.Sprintf("Updated order %s status to %s", order.DisplayOrderID, order.Status),
	})

	return order, nil
}

func (a *orderActivityLogAdapter) UpdateOrderPayment(ctx context.Context, orderPaymentRequest *types.CreateOrderPaymentRequest) (*types.GetOrderResponse, error) {
	order, err := a.orderService.UpdateOrderPayment(ctx, orderPaymentRequest)
	if err != nil {
		return nil, err
	}

	a.activityLogService.CreateActivity(ctx, &types.LogActivityRequest{
		Requestor:   orderPaymentRequest.Requestor,
		Event:       "paid",
		Subject:     "order",
		SubjectId:   order.ID,
		Description: fmt.Sprintf("Paid order %s", order.DisplayOrderID),
	})

	return order, nil
}
