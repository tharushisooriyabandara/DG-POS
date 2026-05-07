package service

import (
	"cmp"
	"context"
	"database/sql"
	"encoding/csv"
	"errors"
	"fmt"
	"io"
	"strconv"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/env"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
	"github.com/Delivergate-Dev/pos-service-golang/service/generate"
	"github.com/Delivergate-Dev/pos-service-golang/service/transaction"
	"github.com/Delivergate-Dev/pos-service-golang/service/usecase"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/Rhymond/go-money"
	"go.uber.org/zap"
)

var (
	ErrOrderCreationFailed = errors.New("failed to create order")
	ErrOrderUpdateFailed   = errors.New("failed to update order")
	ErrOrderNotFound       = errors.New("order not found")
)

type OrderService struct {
	db            *sql.DB
	queries       *db.Queries
	logger        *zap.Logger
	customerCrypt customerCryptoService
	userCrypt     userCryptoService
}

func NewOrderService(logger *zap.Logger, conn *sql.DB, uc userCryptoService, cc customerCryptoService) *OrderService {
	return &OrderService{
		logger:        logger,
		db:            conn,
		customerCrypt: cc,
		userCrypt:     uc,
		queries:       db.New(conn),
	}
}

func (s *OrderService) GetOrder(ctx context.Context, orderID uint64) (*types.GetOrderResponse, error) {
	queries := db.New(s.db)

	result, err := queries.GetOrderById(ctx, orderID)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, ErrOrderNotFound
		}
		return nil, fmt.Errorf("failed to get order: %w", err)
	}

	orderItems, err := queries.GetOrderItemsByOrderID(ctx, int32(orderID))
	if err != nil {
		return nil, fmt.Errorf("failed to get order items: %w", err)
	}

	customer, err := queries.GetCustomerByID(ctx, uint64(result.Order.CustomerID.Int32))
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return nil, fmt.Errorf("failed to get customer: %w", err)
	}

	decryptedCustomer, err := s.customerCrypt.DecryptCustomer(ctx, customer)
	if err != nil {
		return nil, fmt.Errorf("failed to decrypt customer: %w", err)
	}

	shopFees, err := queries.GetShopFeesByOrderID(ctx, int32(orderID))
	if err != nil {
		return nil, fmt.Errorf("failed to get shop fees: %w", err)
	}

	orderTaxes, err := queries.GetOrderSalesTaxes(ctx, int32(orderID))
	if err != nil {
		return nil, fmt.Errorf("failed to get order taxes: %w", err)
	}

	typeId := cmp.Or(uint64(result.Order.OrderSessionID.Int32), orderID)
	orderTransactions, err := queries.GetOrderTransactions(ctx, typeId)
	if err != nil {
		return nil, fmt.Errorf("failed to get order transactions: %w", err)
	}

	totalRefundableBalance, cashRefundableBalance, cardRefundableBalance, err := s.getRefundBalances(ctx, typeId)
	if err != nil {
		return nil, fmt.Errorf("failed to get refund balances: %w", err)
	}

	var salesTotal int32
	var refundTotal int32
	for _, transaction := range orderTransactions {
		if transaction.OrderTransaction.TransactionType == "REFUND" {
			refundTotal += transaction.OrderTransaction.TransactionAmount
		}
		if transaction.OrderTransaction.TransactionType == "SALE" {
			salesTotal += transaction.OrderTransaction.TransactionAmount
		}
	}

	var refundStatus string
	if salesTotal > 0 && salesTotal == refundTotal {
		refundStatus = "Refunded"
	} else if refundTotal > 0 {
		refundStatus = "Partially Refunded"
	}

	orderResp := convert.DbOrderToGetOrderResponse(result)
	orderResp.OrderTaxes = convert.DbOrderTaxesToOrderTaxesResponse(orderTaxes)
	orderResp.ShopFees = convert.DbShopFeesToShopFeesResponse(shopFees)
	orderResp.Transactions = convert.ToOrderTransactionsresp(orderTransactions)
	orderResp.RefundStatus = refundStatus
	orderResp.RefundBalance = float64(salesTotal-refundTotal) / 100.00
	orderResp.RefundBalances = &types.RefundBalanceResponse{
		TotalRefundBalance: float64(totalRefundableBalance) / 100.00,
		CashRefundBalance:  float64(cashRefundableBalance) / 100.00,
		CardRefundBalance:  float64(cardRefundableBalance) / 100.00,
	}
	orderResp.DelivergateCustomer = convert.DBCustomerToCustomerResp(decryptedCustomer)
	orderResp.ShippingDetails = convert.CustomerShippingDetailsToShippingDetailsResponse(decryptedCustomer)

	getPrinterGroupsForItem := func(itemID int32) ([]*types.PrinterGroupsResp, error) {
		printerGroups, err := queries.GetPrinterGroupsByItemID(ctx, itemID)
		if err != nil {
			return nil, fmt.Errorf("failed to get printer groups: %w", err)
		}
		return convert.DbPrinterGroupsToPrinterGroupsResponse(printerGroups), nil
	}

	orderResp.Items, err = convert.DbOrderItemsToGetOrderItemsResponse(orderItems, getPrinterGroupsForItem)
	if err != nil {
		return nil, fmt.Errorf("failed to convert order items: %w", err)
	}

	dp, err := queries.GetDeliveryPlatformById(ctx, uint64(result.Order.PlatformID))
	if err != nil {
		return nil, fmt.Errorf("failed to get delivery platform: %w", err)
	}

	posPlatformID, _ := strconv.Atoi(env.Config.PosPlatformID)
	switch dp.PlatformID.Int32 {
	case int32(posPlatformID):
		if err := setPosOrderDetails(ctx, queries, orderResp); err != nil {
			return nil, err
		}
	case 6, 8: // webshop or table order
		if err := setWebshopOrderDetails(ctx, queries, orderResp); err != nil {
			return nil, err
		}
	}

	return orderResp, nil
}

func (s *OrderService) getRefundBalances(ctx context.Context, typeId uint64) (int32, int32, int32, error) {

	sales, err := s.queries.GetSalesAndRefundByTypeID(ctx, typeId)
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return 0, 0, 0, err
	}

	total := sales.NetSale
	cashBalance := sales.CashSale - sales.CashRefund
	cardBalance := sales.CardSale - sales.CardRefund

	if cashBalance < 0 {
		overflow := -cashBalance
		cashBalance = 0
		cardBalance -= overflow
	}

	return total, cashBalance, cardBalance, nil
}

func (s *OrderService) GetOrders(ctx context.Context, getOrdersRequest *types.GetOrdersRequest) ([]*types.GetOrdersResponse, error) {
	// already validated, so ignoring the error
	fromDate, _ := time.Parse(time.DateTime, getOrdersRequest.FromDate)
	toDate, _ := time.Parse(time.DateTime, getOrdersRequest.ToDate)
	toDate = toDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)

	if getOrdersRequest.ToDate == "" {
		toDate = fromDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)
	}

	// temporary fix to expand the time window.
	// from date = incoming date - 24 hrs.
	// to date = incoming date (or calculated) + 24 hrs
	fromDate = fromDate.Add(-24 * time.Hour)
	toDate = toDate.Add(24 * time.Hour)

	queries := db.New(s.db)

	platformIDs := make([]sql.NullInt32, 0, len(getOrdersRequest.PlatformIds))
	for _, id := range getOrdersRequest.PlatformIds {
		platformIDs = append(platformIDs, sql.NullInt32{Int32: id, Valid: true})
	}

	sortOrder := "asc"
	if getOrdersRequest.SortBy == "desc" {
		sortOrder = "desc"
	}
	dbOrders, err := queries.GetOrders(ctx, &db.GetOrdersParams{
		PlatformIds:    platformIDs,
		ApplyStatus:    sql.NullString{Valid: getOrdersRequest.Status != ""},
		ApplyOutletID:  sql.NullInt32{Valid: getOrdersRequest.OutletID != 0},
		ApplyStartDate: sql.NullTime{Valid: getOrdersRequest.FromDate != ""},
		ApplyEndDate:   sql.NullTime{Valid: getOrdersRequest.ToDate != ""},
		Status:         getOrdersRequest.Status,
		OutletID:       getOrdersRequest.OutletID,
		StartDate:      sql.NullTime{Time: fromDate, Valid: getOrdersRequest.FromDate != ""},
		EndDate:        sql.NullTime{Time: toDate, Valid: getOrdersRequest.ToDate != ""},
		SortBy:         sql.NullString{String: sortOrder, Valid: true},
	})
	if err != nil {
		return nil, err
	}

	for i := range dbOrders {
		if !dbOrders[i].Order.DgposTableID.Valid {
			if tableOrderingsId, _ := strconv.Atoi(dbOrders[i].Order.TableID.String); tableOrderingsId != 0 {
				table, err := queries.GetTableByTableOrderingsID(ctx, sql.NullInt64{Int64: int64(tableOrderingsId), Valid: true})
				if err != nil && !errors.Is(err, sql.ErrNoRows) {
					continue
				}
				dbOrders[i].Order.DgposTableID = sql.NullInt32{Int32: int32(table.ID), Valid: true}
				dbOrders[i].TableName = sql.NullString{String: table.Name, Valid: table.Name != ""}
				dbOrders[i].TableOrderingsID = sql.NullInt64{Int64: table.TableOrderingsID.Int64, Valid: table.TableOrderingsID.Int64 != 0}
			}
		}
	}

	return convert.DbOrdersToGetOrdersResponse(dbOrders), nil
}

func (s *OrderService) GetOrdersAsCsv(ctx context.Context, getOrdersRequest *types.GetOrdersRequest, w io.Writer) error {

	// already validated, so ignoring the error
	fromDate, _ := time.Parse(time.DateTime, getOrdersRequest.FromDate)
	toDate, _ := time.Parse(time.DateTime, getOrdersRequest.ToDate)
	toDate = toDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)

	if getOrdersRequest.ToDate == "" {
		toDate = fromDate.Add(23*time.Hour + 59*time.Minute + 59*time.Second)
	}

	queries := db.New(s.db)

	platformIDs := make([]uint64, 0, len(getOrdersRequest.PlatformIds))
	for _, id := range getOrdersRequest.PlatformIds {
		platformIDs = append(platformIDs, uint64(id))
	}

	sortOrder := "asc"
	if getOrdersRequest.SortBy == "desc" {
		sortOrder = "desc"
	}
	dbOrders, err := queries.GetOrdersForExport(ctx, &db.GetOrdersForExportParams{
		PlatformIds: platformIDs,
		Status:      sql.NullString{String: getOrdersRequest.Status, Valid: getOrdersRequest.Status != ""},
		OutletID:    sql.NullInt32{Int32: getOrdersRequest.OutletID, Valid: getOrdersRequest.OutletID != 0},
		StartDate:   sql.NullTime{Time: fromDate, Valid: true},
		EndDate:     sql.NullTime{Time: toDate, Valid: true},
		SortBy:      sql.NullString{String: sortOrder, Valid: true},
	})
	if err != nil {
		return err
	}

	shop, err := s.queries.GetShopByID(ctx, uint64(getOrdersRequest.OutletID))
	if err != nil {
		return fmt.Errorf("couldn't fetch shop time zone : %w", err)
	}

	// time should be in shop timezone in csv
	loc, err := time.LoadLocation(shop.Timezone)
	if err != nil {
		return fmt.Errorf("couldn't load shop time zone : %w", err)
	}

	cw := csv.NewWriter(w)

	headers := []string{
		"Display Order ID",
		"Platform Name",
		"Date/Time",
		"Total Amount",
		"Status",
		"Order Type",
		"Shift ID",
		"Customer Name",
		"Customer Contact Number",
		"Total Tax",
		"Tip",
		"Service Charges & Fees",
		"Discount",
		"Payment Mode",
		"Payment Types",
	}
	cw.Write(headers)

	for _, row := range dbOrders {

		decryptedCustomer, err := s.customerCrypt.DecryptCustomer(ctx, &db.Customer{
			Phone:      row.CustomerContactNumber,
			KeyIDPhone: row.CustomerKeyIDContactNumber.Int32,
		})
		if err != nil {
			return fmt.Errorf("failed to decrypt customer: %w", err)
		}

		phoneNumber := decryptedCustomer.Phone.String
		if row.CustomerCountryCode.Valid {
			phoneNumber = fmt.Sprintf("(%s) %s", row.CustomerCountryCode.String, decryptedCustomer.Phone.String)
		}

		total := money.New(int64(row.Order.TotalAmount), shop.CurrencyCode)
		totalFee := money.New(int64(row.Order.TotalFee.Int32), shop.CurrencyCode)
		discount := money.New(int64(row.Order.Discount), shop.CurrencyCode)

		record := []string{
			row.Order.DisplayOrderID.String,
			row.PlatformName.String,
			row.Order.DeliveryDateTime.In(loc).Format("02/01/2006"),
			fmt.Sprintf("%s %.2f", total.Currency().Code, total.AsMajorUnits()),
			row.Order.Status,
			row.Order.ShippingMethod.String,
			strconv.Itoa(int(row.CashDrawerSessionID)),
			row.Order.CustomerName.String,
			phoneNumber,
			row.Order.TotalTax.String,
			row.Order.Tip,
			fmt.Sprintf("%s %.2f", totalFee.Currency().Code, totalFee.AsMajorUnits()),
			fmt.Sprintf("%s %.2f", discount.Currency().Code, discount.AsMajorUnits()),
			row.Order.PaymentMode.String,
			row.PaymentTypes.String,
		}

		cw.Write(record)
		cw.Flush()
	}

	cw.Flush()
	if err := cw.Error(); err != nil {
		return fmt.Errorf("failed to write csv: %w", err)
	}

	return nil
}

func setPosOrderDetails(ctx context.Context, queries *db.Queries, orderResp *types.GetOrderResponse) error {
	paymentInfo, err := queries.GetDgPaymentStatusByOrderId(ctx, int32(orderResp.ID))
	if err != nil {
		return fmt.Errorf("failed to get dg pos order payment status: %w", err)
	}

	table, err := queries.GetTableByID(ctx, uint64(orderResp.TableID))
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return fmt.Errorf("failed to get dg pos table name: %w", err)
	}

	var shippingDetails *db.DgPosOrderLocation
	if orderResp.ShippingMethod == "DELIVERY" {
		shippingDetails, err = queries.GetDgPosOrderLocation(ctx, int32(orderResp.ID))
		if err != nil && !errors.Is(err, sql.ErrNoRows) {
			return fmt.Errorf("failed to get dg pos order location: %w", err)
		}
	}

	orderResp.ShippingDetails = convert.DGShippingDetailsToShippingDetailsResponse(shippingDetails)
	orderResp.TableName = table.Name
	orderResp.TableOrderingsID = int32(table.TableOrderingsID.Int64)
	orderResp.PaymentStatus = paymentInfo.Status.String
	orderResp.PaymentType = paymentInfo.PaymentType.String
	return nil
}

func setWebshopOrderDetails(ctx context.Context, queries *db.Queries, orderResp *types.GetOrderResponse) error {
	orderId := sql.NullInt32{Int32: int32(orderResp.ID), Valid: true}
	paymentInfo, err := queries.GetWebshopPaymentStatusByOrderId(ctx, orderId)
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return fmt.Errorf("failed to get webshop order payment status: %w", err)
	}

	var dgPosTable *db.DgPosTable
	if orderResp.TableID != 0 {
		dgPosTable, err = queries.GetTableByID(ctx, uint64(orderResp.TableID))
		if err != nil && !errors.Is(err, sql.ErrNoRows) {
			return fmt.Errorf("failed to get table info by pos table id for webshop order: %w", err)
		}
	} else {
		dgPosTable, err = queries.GetTableByTableOrderingsID(ctx, sql.NullInt64{Int64: int64(orderResp.TableOrderingsID), Valid: true})
		if err != nil && !errors.Is(err, sql.ErrNoRows) {
			return fmt.Errorf("failed to get table info by table orderings id for webshop order: %w", err)
		}
	}

	var shippingDetails *db.WebshopOrderLocation
	if orderResp.ShippingMethod == "DELIVERY" {
		shippingDetails, err = queries.GetWebshopOrderLocation(ctx, sql.NullInt64{Int64: int64(orderResp.ID), Valid: true})
		if err != nil && !errors.Is(err, sql.ErrNoRows) {
			return fmt.Errorf("failed to get webshop order location: %w", err)
		}
	}

	if orderResp.DiscountType == "voucher" {
		orderResp.VoucherDiscount = orderResp.Discount
		orderResp.Discount = 0
	}

	orderResp.PaymentStatus = paymentInfo.PaymentStatus.String
	orderResp.PaymentType = paymentInfo.PaymentType.String
	orderResp.TableID = int32(dgPosTable.ID)
	orderResp.TableOrderingsID = int32(dgPosTable.TableOrderingsID.Int64)
	orderResp.TableName = dgPosTable.Name
	orderResp.ShippingDetails = convert.WebshopShippingDetailsToShippingDetailsResponse(shippingDetails)
	return nil
}

func (s *OrderService) UpdateOrderStatus(ctx context.Context, updateReq *types.UpdateOrderStatusRequest) (*types.GetOrderResponse, error) {

	queries := db.New(s.db)

	// verifications
	result, err := queries.GetOrderById(ctx, updateReq.OrderID)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, fmt.Errorf("%w: %v", ErrOrderNotFound, err)
		}
		return nil, err
	}
	order := result.Order

	// get order status timestamps
	orderTimestamps, err := queries.GetOrderStatusTimestamps(ctx, int32(order.ID))
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return nil, fmt.Errorf("failed to get order status timestamps: %w", err)
	}

	// conversions
	dbOrder := &convert.Order{
		Order:           &order,
		OrderTimestamps: orderTimestamps,
	}

	// use cases
	if err := usecase.Apply(ctx, queries, dbOrder,
		usecase.UpdateRestriction,
		usecase.OrderTimestamps(updateReq.Status),
	); err != nil {
		return nil, err
	}

	// transactions
	var updateTransaction func(ctx context.Context, qtx *db.Queries) error
	switch {
	case order.ShippingMethod.String == "DINE-IN" && updateReq.Status == "SERVED":
		updateTransaction = transaction.Build(
			transaction.UpdateOrderStatus(dbOrder, updateReq.Status, updateReq.Note),
			transaction.SetTableStatus(dbOrder, updateReq.Status),
		)
	case order.ShippingMethod.String == "DINE-IN" && (updateReq.Status == "COMPLETED" || updateReq.Status == "CANCELLED"):
		updateTransaction = transaction.Build(
			transaction.UpdateOrderStatus(dbOrder, handleCancelledStatus(updateReq.Status), updateReq.Note),
			transaction.SetTableStatus(dbOrder, "AVAILABLE"),
		)

	default:
		updateTransaction = transaction.UpdateOrderStatus(dbOrder, handleCancelledStatus(updateReq.Status), updateReq.Note)
	}

	if err := transaction.Exec(ctx, s.db, updateTransaction); err != nil {
		return nil, err
	}

	orderResp, err := queries.GetOrderById(ctx, order.ID)
	if err != nil {
		return nil, fmt.Errorf("failed to get order: %w", err)
	}

	return convert.DbOrderToGetOrderResponse(orderResp), nil
}

func handleCancelledStatus(status string) string {
	if status == "CANCELLED" {
		return "CANCELED" // single L
	}
	return status
}

func (s *OrderService) CreateOrder(ctx context.Context, orderRequest *types.CreateOrderRequest) (*types.GetOrderResponse, error) {

	queries := db.New(s.db)

	// conversions
	order := convert.OrderRequestToOrder(orderRequest)
	if err := usecase.Apply(ctx, queries, order,
		// verifications
		// usecase.PaymentAmount,
		usecase.TableAvailability,
		usecase.ItemAvailability,
		usecase.VerifyCustomer,

		// order use cases
		usecase.GuestCustomer,
		usecase.OrderCommission,
		usecase.OrderPayments,
		usecase.OrderLocation(orderRequest.OrderReceiverAddressID, s.customerCrypt),
		usecase.OrderItemModifierString(orderRequest.OrderItems),
	); err != nil {
		return nil, err
	}

	// transactions
	orderTransaction := transaction.CreateOrder(order)
	if err := transaction.Exec(ctx, s.db, orderTransaction); err != nil {
		return nil, fmt.Errorf("%w: %v", ErrOrderCreationFailed, err)
	}

	orderResp, err := queries.GetOrderById(ctx, order.ID)
	if err != nil {
		return nil, fmt.Errorf("failed to get order: %w", err)
	}

	return convert.DbOrderToGetOrderResponse(orderResp), nil
}

func (s *OrderService) UpdateOrder(ctx context.Context, orderID uint64, orderUpdateRequest *types.UpdateOrderRequest) (*types.GetOrderResponse, error) {

	queries := db.New(s.db)

	result, err := queries.GetOrderById(ctx, orderID)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, fmt.Errorf("%w: %v", ErrOrderNotFound, err)
		}
		return nil, err
	}
	existingOrder := &result.Order

	// conversions
	order := convert.OrderUpdateRequestToOrder(existingOrder, orderUpdateRequest)

	existingOrderTableId := existingOrder.DgposTableID.Int32
	newOrderTableId := order.DgposTableID.Int32
	if err := usecase.Apply(ctx, queries, order,
		// verifications
		usecase.If(existingOrderTableId != newOrderTableId, usecase.TableAvailability),
		usecase.ItemAvailability,

		// order use cases
		usecase.GuestCustomer,
		usecase.OrderCommission,
		usecase.OrderPayments,
		usecase.OrderItemModifierString(orderUpdateRequest.OrderItems),
	); err != nil {
		return nil, fmt.Errorf("%w: %v", ErrOrderUpdateFailed, err)
	}

	// transactions
	updateTransaction := transaction.OrderUpdate(existingOrder, order)
	if err := transaction.Exec(ctx, s.db, updateTransaction); err != nil {
		return nil, fmt.Errorf("%w: %v", ErrOrderUpdateFailed, err)
	}

	orderResp, err := queries.GetOrderById(ctx, order.ID)
	if err != nil {
		return nil, fmt.Errorf("failed to get order: %w", err)
	}

	return convert.DbOrderToGetOrderResponse(orderResp), nil
}

func (s *OrderService) UpdateOrderPayment(ctx context.Context, orderPaymentRequest *types.CreateOrderPaymentRequest) (*types.GetOrderResponse, error) {

	queries := db.New(s.db)

	result, err := queries.GetOrderById(ctx, orderPaymentRequest.OrderID)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, fmt.Errorf("%w: %v", ErrOrderNotFound, err)
		}
		return nil, err
	}
	existingOrder := &result.Order

	// conversions
	order := &convert.Order{
		Order:            existingOrder,
		OrderPayments:    convert.OrderPaymentsRequestToOrderPayments(orderPaymentRequest),
		OrderTransaction: convert.OrderTransactions(orderPaymentRequest.Payments),
		OrderTimestamps: &db.DgPosOrderStatusTimestamp{
			Queue:     existingOrder.CreatedAt,
			Preparing: existingOrder.CreatedAt,
			Served:    existingOrder.CreatedAt,
			Delivered: existingOrder.CreatedAt,
			Completed: sql.NullTime{Time: time.Now().UTC(), Valid: true},
			CreatedAt: existingOrder.CreatedAt,
			UpdatedAt: sql.NullTime{Time: time.Now().UTC(), Valid: true},
		},
	}
	paymentModeString := generate.PaymentModeString(orderPaymentRequest.Payments)
	order.PaymentMode = sql.NullString{String: paymentModeString, Valid: paymentModeString != ""}

	if err := usecase.Apply(ctx, queries, order,
		// usecase.PaymentAmount,
		usecase.CanBePaid,
	); err != nil {
		return nil, fmt.Errorf("%w: %v", ErrOrderCreationFailed, err)
	}

	// transactions
	updateTransaction := transaction.OrderPaymentUpdate(order)
	if err := transaction.Exec(ctx, s.db, updateTransaction); err != nil {
		return nil, fmt.Errorf("%w: %v", ErrOrderUpdateFailed, err)
	}

	orderResp, err := queries.GetOrderById(ctx, order.ID)
	if err != nil {
		return nil, fmt.Errorf("failed to get order: %w", err)
	}

	return convert.DbOrderToGetOrderResponse(orderResp), nil
}

func (s *OrderService) RefundOrder(ctx context.Context, refund *types.RefundOrderRequest) (*types.GetOrderResponse, error) {
	_, err := s.queries.GetOrderById(ctx, refund.OrderID)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, fmt.Errorf("%w: %v", ErrOrderNotFound, err)
		}
		return nil, err
	}

	if err := s.createRefund(ctx,
		refund.OrderID,
		refund.RefundMode,
		int32(refund.RefundAmount*100),
		refund.Reason,
	); err != nil {
		return nil, err
	}

	if err := s.CreateOrderTaxRefunds(ctx, &db.Order{ID: refund.OrderID}, int32(refund.RefundAmount*100)); err != nil {
		return nil, err
	}

	return s.GetOrder(ctx, refund.OrderID)
}

func (s *OrderService) createRefund(
	ctx context.Context,
	orderID uint64,
	mode string,
	amount int32,
	reason string,
) error {

	transactionID, err := s.queries.CreateOrderTransaction(ctx, &db.CreateOrderTransactionParams{
		TypeID:            orderID,
		Type:              "ORDER",
		TransactionType:   "REFUND",
		TransactionMode:   sql.NullString{String: mode, Valid: true},
		TransactionAmount: amount,
	})
	if err != nil {
		return fmt.Errorf("failed to create %s refund transaction: %w", mode, err)
	}

	_, err = s.queries.CreateOrderRefund(ctx, &db.CreateOrderRefundParams{
		TypeID:        orderID,
		Type:          "ORDER",
		TransactionID: uint64(transactionID),
		RefundAmount:  amount,
		RefundMode:    mode,
		Reason:        sql.NullString{String: reason, Valid: reason != ""},
	})
	if err != nil {
		return fmt.Errorf("failed to create %s refund: %w", mode, err)
	}

	return nil
}

func (s *OrderService) CreateOrderTaxRefunds(ctx context.Context, order *db.Order, totalRefundingAmount int32) error {
	orderTaxes, err := usecase.TaxRefund(ctx, s.queries, order, totalRefundingAmount)
	if err != nil {
		return fmt.Errorf("failed to create tax refunds: %w", err)
	}
	for _, tax := range orderTaxes {
		if err := s.queries.InsertOrderTaxable(ctx, &db.InsertOrderTaxableParams{
			OrderID:       int32(order.ID),
			TaxRate:       tax.TaxRate,
			TaxCode:       tax.TaxCode,
			TaxAmount:     tax.TaxAmount,
			TaxableAmount: tax.TaxableAmount,
			Type:          tax.Type,
		}); err != nil {
			return fmt.Errorf("failed to create tax refunds: %w", err)
		}
	}
	return nil
}
