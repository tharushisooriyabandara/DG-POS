package wrap

import (
	"context"
	"io"
	"time"

	"github.com/Delivergate-Dev/pos-service-golang/types"
	"golang.org/x/exp/constraints"
)

type activityLogService interface {
	CreateActivity(ctx context.Context, activityLogReq *types.LogActivityRequest) error
}

type authService interface {
	Authenticate(ctx context.Context, loginRequest types.LoginRequest) (*types.SessionUser, string, string, error)
	Refresh(ctx context.Context, refreshToken string) (string, string, error)
	InvalidateRefreshToken(ctx context.Context, user types.SessionUser) error
	VerifyPin(ctx context.Context, verifyPinRequest types.VerifyPinRequest) error
	ValidateAccessToken(ctx context.Context, tokenString string) (*types.SessionUser, error)
}

type customerService interface {
	GetCustomers(ctx context.Context) ([]*types.GetCustomersResponse, error)
	GetCustomerDetails(ctx context.Context, customerID uint64) (*types.GetCustomerDetailsResponse, error)
	CreateCustomer(ctx context.Context, createCustomerRequest *types.CreateCustomerRequest) (*types.GetCustomersResponse, error)
	CreateCustomerAddress(ctx context.Context, createCustomerAddressRequest *types.CreateCustomerAddressRequest) error
	UpdateCustomer(ctx context.Context, updateCustomerRequest *types.UpdateCustomerRequest) error
}

type orderService interface {
	CreateOrder(ctx context.Context, orderRequest *types.CreateOrderRequest) (*types.GetOrderResponse, error)
	UpdateOrder(ctx context.Context, orderID uint64, orderRequest *types.UpdateOrderRequest) (*types.GetOrderResponse, error)
	UpdateOrderStatus(ctx context.Context, updateReq *types.UpdateOrderStatusRequest) (*types.GetOrderResponse, error)
	UpdateOrderPayment(ctx context.Context, orderPaymentRequest *types.CreateOrderPaymentRequest) (*types.GetOrderResponse, error)
	RefundOrder(ctx context.Context, refundOrderRequest *types.RefundOrderRequest) (*types.GetOrderResponse, error)
	GetOrders(ctx context.Context, getOrdersRequest *types.GetOrdersRequest) ([]*types.GetOrdersResponse, error)
	GetOrdersAsCsv(ctx context.Context, getOrdersRequest *types.GetOrdersRequest, writer io.Writer) error
	GetOrder(ctx context.Context, orderID uint64) (*types.GetOrderResponse, error)
}

type cashDrawerService interface {
	RecordCashMovement(ctx context.Context, req types.RecordCashMovementRequest) error
}

func nullableStr(s string) *string {
	if s == "" {
		return nil
	}
	return &s
}

func nullableNum[T constraints.Integer](v T) *T {
	if v == 0 {
		return nil
	}
	return &v
}

func nullableTime(t time.Time) *string {
	if t.IsZero() {
		return nil
	}
	s := t.Format("2006-01-02 15:04:05")
	return &s
}

func nullableBool(b bool) *bool {
	if !b {
		return nil
	}
	return &b
}

type serializedContent map[string]any

type messageBody struct {
	Data data `json:"data"`
}

type data struct {
	Command string `json:"command"`
}
