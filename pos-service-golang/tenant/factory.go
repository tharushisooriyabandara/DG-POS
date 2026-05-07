package tenant

import (
	"context"
	"fmt"
	"time"

	"github.com/Delivergate-Dev/pos-service-golang/database"
	"github.com/Delivergate-Dev/pos-service-golang/env"
	"github.com/Delivergate-Dev/pos-service-golang/logger"
	"github.com/Delivergate-Dev/pos-service-golang/service"
	"github.com/Delivergate-Dev/pos-service-golang/service/crypt"
	"github.com/Delivergate-Dev/pos-service-golang/service/wrap"
	"go.uber.org/zap"
)

var factory *tenantFactory

const maintenanceInterval = 59 * time.Minute

type tenantFactory struct {
	cache    map[string]*Runtime
	requests chan tenantRequest
	shutdown chan chan struct{}
}

func InitializeTenantFactory() {
	factory = &tenantFactory{
		cache:    make(map[string]*Runtime),
		requests: make(chan tenantRequest),
		shutdown: make(chan chan struct{}),
	}

	go factory.worker(time.NewTicker(maintenanceInterval))

	logger.Info("Tenant factory initialized")
}

func ShutdownFactory() {
	done := make(chan struct{})
	factory.shutdown <- done
	<-done

	logger.Info("Tenant factory shutdown complete")
}

func (f *tenantFactory) worker(ticker *time.Ticker) {
	for {
		select {
		case req := <-f.requests:
			f.handleRequest(req)

		case <-ticker.C:
			f.maintenance()

		case done := <-f.shutdown:
			ticker.Stop()

			for _, runtime := range f.cache {
				runtime.Close()
			}

			done <- struct{}{}
			return
		}
	}
}

type tenantRequest struct {
	ctx        context.Context
	tenantCode string
	response   chan tenantResponse
}

type tenantResponse struct {
	runtime *Runtime
	err     error
}

func (f *tenantFactory) handleRequest(req tenantRequest) {
	runtime, ok := f.cache[req.tenantCode]
	if ok {
		runtime.lastAccess = time.Now().UTC()
		req.response <- tenantResponse{runtime: runtime}
		return
	}

	runtime, err := createRuntime(req.ctx, req.tenantCode)
	if err != nil {
		req.response <- tenantResponse{err: err}
		return
	}

	f.cache[req.tenantCode] = runtime
	req.response <- tenantResponse{runtime: runtime}
}

func GetRuntime(ctx context.Context, tenantCode string) (*Runtime, error) {
	response := make(chan tenantResponse)
	factory.requests <- tenantRequest{ctx: ctx, tenantCode: tenantCode, response: response}
	resp := <-response
	return resp.runtime, resp.err
}

func (f *tenantFactory) maintenance() {

	if len(f.cache) == 0 {
		return
	}

	logger.Info("Starting maintenance routine")

	var tenantsToRemove []string
	for tenantCode, runtime := range f.cache {
		// check pos enabled
		tenantInfo, err := database.GetTenantInfo(context.Background(), tenantCode)
		if err != nil {
			tenantsToRemove = append(tenantsToRemove, tenantCode)
			logger.Info(err.Error(), zap.String("tenant", tenantCode), zap.Error(err))
			continue
		}

		if !tenantInfo.EnablePos {
			tenantsToRemove = append(tenantsToRemove, tenantCode)
			logger.Info("POS disabled tenant found", zap.String("tenant", tenantCode))
			continue
		}

		// check database connection
		if err := runtime.db.Ping(); err != nil {
			tenantsToRemove = append(tenantsToRemove, tenantCode)
			logger.Warn("unreachable tenant found", zap.String("tenant", tenantCode))
			continue
		}

		// check inactivity
		if time.Now().UTC().Sub(runtime.lastAccess) > maintenanceInterval {
			tenantsToRemove = append(tenantsToRemove, tenantCode)
			logger.Info("inactive tenant found", zap.String("tenant", tenantCode))
			continue
		}
	}

	for _, tenantCode := range tenantsToRemove {
		if runtime, exists := f.cache[tenantCode]; exists {
			logger.Info("Tenant removed from cache", zap.String("tenant", tenantCode))
			runtime.Close()
			delete(f.cache, tenantCode)
		}
	}

	logger.Info("Maintenance routine completed")
}

func createRuntime(ctx context.Context, tenantCode string) (*Runtime, error) {
	masterDB := database.GetMasterDB()

	tenantInfo, err := database.GetTenantInfo(ctx, tenantCode)
	if err != nil {
		return nil, fmt.Errorf("failed to get tenant info: %w", err)
	}

	if !tenantInfo.EnablePos {
		return nil, fmt.Errorf("tenant is not enabled for pos")
	}

	tenantDB, err := database.ConnectTenantDB(ctx, tenantCode)
	if err != nil {
		return nil, fmt.Errorf("failed to connect to tenant database: %w", err)
	}

	// infra services
	logger := logger.With(zap.String("tenant", tenantCode))
	activityLogService := service.NewActivityLogService(logger, tenantDB)
	customerCryptoService := crypt.NewCustomerCryptoService(masterDB, tenantInfo.EnableEncryption)
	userCryptoService := crypt.NewUserCryptoService(masterDB, tenantInfo.EnableEncryption)
	cashDrawerService := service.NewCashDrawerService(logger, tenantDB, userCryptoService)

	return &Runtime{
		tenantCode: tenantCode,
		db:         tenantDB,
		logger:     logger,
		lastAccess: time.Now().UTC(),

		// services
		UserService: service.NewUserService(
			tenantCode,
			tenantDB,
			logger,
			userCryptoService,
		),

		CustomerService: wrap.NewCustomerSQSWrapper(
			env.Config.Environment == "test",
			tenantDB,
			logger,
			wrap.NewCustomerActivityLogger(
				activityLogService,
				service.NewCustomerService(
					tenantDB,
					logger,
					customerCryptoService,
				),
			),
		),

		OrderService: wrap.NewOrderTransactionsSQSWrapper(
			env.Config.Environment == "test",
			tenantDB,
			logger,
			wrap.NewOrderSqsWrapper(
				env.Config.Environment == "test",
				tenantDB,
				logger,
				customerCryptoService,
				wrap.NewOrderActivityLogger(
					activityLogService,
					wrap.NewCashDrawerAdapter(tenantDB, cashDrawerService,
						service.NewOrderService(
							logger,
							tenantDB,
							userCryptoService,
							customerCryptoService,
						),
					),
				),
			),
		),

		AuthService: wrap.NewUserActivityLogger(
			activityLogService,
			service.NewAuthService(
				tenantDB,
				logger,
			),
		),

		ActivityLogService:      activityLogService,
		MenuService:             service.NewItemCategoryService(logger, tenantDB),
		ShopService:             service.NewShopService(logger, tenantDB),
		TableService:            service.NewTableService(logger, tenantDB),
		DeliveryPlatformService: service.NewDeliveryPlatformService(logger, tenantDB),
		ReportsService:          service.NewReportsService(logger, tenantDB),
		CashDrawerService:       cashDrawerService,
		IncomingOrderService:    service.NewIncomingOrderService(tenantDB),
		TemporaryPaymentService: service.NewTemporaryPaymentService(tenantDB),
	}, nil
}
