package tenant

import (
	"database/sql"
	"time"

	"go.uber.org/zap"
)

type Runtime struct {
	tenantCode string
	logger     *zap.Logger
	db         *sql.DB
	lastAccess time.Time

	// services
	UserService             userService
	AuthService             authService
	CustomerService         customerService
	MenuService             itemCategoryService
	OrderService            orderService
	IncomingOrderService    incomingOrderService
	ShopService             shopService
	TableService            tableService
	DeliveryPlatformService deliveryPlatformService
	ReportsService          reportsService
	ActivityLogService      activityLogService
	CashDrawerService       cashDrawerService
	TemporaryPaymentService temporaryPaymentService
}

func (t *Runtime) TenantCode() string {
	return t.tenantCode
}

func (t *Runtime) Close() {
	t.logger.Sync()
	t.db.Close()
}
