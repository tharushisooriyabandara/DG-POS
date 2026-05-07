package tenant

import (
	"context"
	"encoding/json"
	"io"

	"github.com/Delivergate-Dev/pos-service-golang/types"
)

type activityLogService interface {
	CreateActivity(ctx context.Context, activityLogReq *types.LogActivityRequest) error
}

type userService interface {
	GetUsers(ctx context.Context, getUsersRequest *types.GetUsersRequest) ([]*types.GetUsersResponse, error)
	GetUser(ctx context.Context, userID uint64) (*types.GetUserResponse, error)
	ChangePin(ctx context.Context, changePinRequest types.ChangePinRequest) error
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

type itemCategoryService interface {
	GetItemCategoriesByID(ctx context.Context, shopId, brandId, mainMenuId uint64) (json.RawMessage, error)
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

type incomingOrderService interface {
	UpdateOrderUserID(ctx context.Context, updateReq *types.UpdateOrderUserIDRequest) error
}

type shopService interface {
	GetShopInfo(ctx context.Context, shopCode string, brandId int32) (*types.GetShopResponse, error)
	GetShopConfig(ctx context.Context, req *types.GetShopConfigRequest) (*types.GetShopConfigResponse, error)
	UpdateShopConfig(ctx context.Context, req *types.UpdateShopConfigRequest) error
}

type tableService interface {
	GetTables(ctx context.Context, req types.QueryFilteredRequest) ([]*types.GetTablesResponse, error)
	UpdateTableStatus(ctx context.Context, req *types.UpdateTableStatusRequest) error
}

type deliveryPlatformService interface {
	GetDeliveryPlatforms(ctx context.Context, req types.QueryFilteredRequest) ([]*types.GetDpResponse, error)
}

type reportsService interface {
	GetShiftInfo(ctx context.Context, req types.GetShiftInfoRequest) (*types.UserShiftInfoResponse, error)
	GetShopShiftInfo(ctx context.Context, req types.GetShopShiftInfoRequest) (*types.ShopShiftInfoResponse, error)
}

type cashDrawerService interface {
	GetActiveCashDrawerSessionInfo(ctx context.Context, req types.GetActiveCashDrawerSessionInfoRequest) (*types.GetActiveCashDrawerSessionInfoResp, error)
	GetCashDrawerTransactionHistory(ctx context.Context, req types.GetCashDrawerTransactionHistoryRequest) ([]*types.GetCashDrawerTransactionHistoryResp, error)
	GetCashDrawerSession(ctx context.Context, sessionID uint64) (*types.GetCashDrawerSessionsResp, error)
	GetCashDrawerSessions(ctx context.Context, req types.GetCashDrawerSessionsRequest) ([]*types.GetCashDrawerSessionsResp, error)
	CreateCashDrawer(ctx context.Context, req types.CreateCashDrawerRequest) error
	OpenCashDrawerSession(ctx context.Context, req types.OpenCashDrawerSessionRequest) error
	CloseCashDrawerSession(ctx context.Context, req types.CloseCashDrawerSessionRequest) error
	RecordCashMovement(ctx context.Context, req types.RecordCashMovementRequest) error
}

type temporaryPaymentService interface {
	CreateDgPosTmpPayment(ctx context.Context, payment *types.DgPosTmpPaymentRequest) error
	GetDgPosTmpPayment(ctx context.Context, orderID string) ([]*types.DgPosTmpPaymentResponse, error)
}
