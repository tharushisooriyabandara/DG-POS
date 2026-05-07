package wrap

import (
	"context"
	"database/sql"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"regexp"
	"strings"
	"unicode/utf8"

	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	awssqs "github.com/Delivergate-Dev/pos-service-golang/mq/aws-sqs"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/elliotchance/phpserialize"
	"go.uber.org/zap"
)

type customerCryptoService interface {
	DecryptCustomer(ctx context.Context, customer *db.Customer) (*db.Customer, error)
}

type orderSqsWrapper struct {
	orderService
	db     *sql.DB
	logger *zap.Logger
	ccs    customerCryptoService
	bypass bool
}

func NewOrderSqsWrapper(bypass bool, db *sql.DB, logger *zap.Logger, ccs customerCryptoService, service orderService) orderService {
	return &orderSqsWrapper{
		orderService: service,
		db:           db,
		logger:       logger,
		ccs:          ccs,
		bypass:       bypass,
	}
}

func (o *orderSqsWrapper) UpdateOrderStatus(ctx context.Context, updateReq *types.UpdateOrderStatusRequest) (*types.GetOrderResponse, error) {
	order, err := o.orderService.UpdateOrderStatus(ctx, updateReq)
	if err != nil {
		return nil, err
	}

	if o.ServiceBypassed() || !o.shouldSentToQueue(order.Status) {
		return order, nil
	}

	if err := o.sendOrderSqsMessage(ctx, order, updateReq.Requestor.TenantCode); err != nil {
		o.logger.Error("Failed to send order sqs message after order status update", zap.Error(err))
	}

	return order, nil
}

func (o *orderSqsWrapper) UpdateOrderPayment(ctx context.Context, orderPaymentRequest *types.CreateOrderPaymentRequest) (*types.GetOrderResponse, error) {
	order, err := o.orderService.UpdateOrderPayment(ctx, orderPaymentRequest)
	if err != nil {
		return nil, err
	}

	if o.ServiceBypassed() || !o.shouldSentToQueue(order.Status) {
		return order, nil
	}

	if err := o.sendOrderSqsMessage(ctx, order, orderPaymentRequest.Requestor.TenantCode); err != nil {
		o.logger.Error("Failed to send order sqs message after order payment update", zap.Error(err))
	}

	return order, nil
}

func (o *orderSqsWrapper) sendOrderSqsMessage(ctx context.Context, order *types.GetOrderResponse, masterTenantCode string) error {
	body, err := o.createSqsMessageBody(ctx, order.ID, masterTenantCode)
	if err != nil {
		return err
	}

	awssqs.Send(ctx, awssqs.Message{
		QueueName:       "orders",
		MessageBody:     string(body),
		MessageGroupId:  order.RemoteOrderID,
		DeduplicationID: fmt.Sprintf("%s%d%d", masterTenantCode, order.ID, time.Now().UTC().Unix()),
	})

	return nil
}

func (o *orderSqsWrapper) createSqsMessageBody(ctx context.Context, orderID uint64, masterTenantCode string) (json.RawMessage, error) {
	queries := db.New(o.db)
	row, err := queries.GetOrderById(ctx, orderID)
	if err != nil {
		return nil, fmt.Errorf("failed to get order: %w", err)
	}

	shop, err := queries.GetShopByID(ctx, uint64(row.Order.ShopID))
	if err != nil {
		return nil, fmt.Errorf("failed to get shop: %w", err)
	}

	dp, err := queries.GetDeliveryPlatformById(ctx, uint64(row.Order.PlatformID))
	if err != nil {
		return nil, fmt.Errorf("failed to get delivery platform: %w", err)
	}

	webshopBrand, err := queries.GetWebshopBrandById(ctx, uint32(dp.WebshopBrandID.Int32))
	if err != nil {
		return nil, fmt.Errorf("failed to get webshop brand: %w", err)
	}

	commission, err := queries.GetOrderCommission(ctx, int64(orderID))
	if err != nil {
		return nil, fmt.Errorf("failed to get order commission: %w", err)
	}

	customer, err := queries.GetCustomerByID(ctx, uint64(row.Order.CustomerID.Int32))
	if err != nil {
		return nil, fmt.Errorf("failed to get customer: %w", err)
	}
	decryptedCustomer, err := o.ccs.DecryptCustomer(ctx, customer)
	if err != nil {
		return nil, fmt.Errorf("failed to decrypt customer: %w", err)
	}

	orderShopFees, err := queries.GetShopFeesByOrderID(ctx, int32(orderID))
	if err != nil {
		return nil, fmt.Errorf("failed to get order shop fees: %w", err)
	}

	orderTaxes, err := queries.GetOrderSalesTaxes(ctx, int32(orderID))
	if err != nil {
		return nil, fmt.Errorf("failed to get order taxes: %w", err)
	}

	orderItems, err := queries.GetOrderItemsByOrderID(ctx, int32(orderID))
	if err != nil {
		return nil, fmt.Errorf("failed to get order items: %w", err)
	}

	orderItemTaxes, err := queries.GetOrderItemTaxes(ctx, int32(orderID))
	if err != nil {
		return nil, fmt.Errorf("failed to get order item taxes: %w", err)
	}

	content := serializedContent{
		"order": serializedContent{
			"tenant_code": shop.Code,
			"order": newOrderData(
				row,
				shop.Code,
				masterTenantCode,
				commission,
				webshopBrand,
				decryptedCustomer,
			),
			"order_taxes":      newOrderTaxesData(orderTaxes),
			"order_items":      newOrderItemsData(orderItems),
			"order_shop_fees":  newOrderShopFeesData(orderShopFees),
			"order_item_taxes": newOrderItemTaxesData(orderItemTaxes),
		},
	}

	phpSerialized, err := phpserialize.Marshal(content, &phpserialize.MarshalOptions{})
	if err != nil {
		return nil, fmt.Errorf("failed to serialize customer message: %w", err)
	}

	dataMsg := messageBody{
		Data: data{
			Command: string(phpSerialized),
		},
	}

	dataMsgBody, err := json.Marshal(dataMsg)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal data message: %w", err)
	}

	return dataMsgBody, nil
}

func newOrderData(row *db.GetOrderByIdRow, shopCode, masterTenantCode string, commission *db.OrderCommission, webshopBrand *db.WebshopBrand, customer *db.Customer) serializedContent {
	return serializedContent{
		"id":                     row.Order.ID,
		"master_tenant_code":     masterTenantCode,
		"tenant_code":            shopCode,
		"delivery_platform_id":   row.Order.PlatformID,
		"delivery_platform_name": nullableStr(row.DpName.String),
		"platform_id":            nullableNum(row.PlatformID.Int64),
		"platform_name":          nullableStr(row.PlatformName.String),
		"remote_order_id":        row.Order.RemoteOrderID,
		"delivery_date_time":     row.Order.DeliveryDateTime.Format("2006-01-02 15:04:05"),
		"total_amount":           row.Order.TotalAmount,
		"status":                 row.Order.Status,
		"note":                   nullableStr(row.Order.Note.String),
		"customer_name":          nullableStr(fmt.Sprintf("%s %s", customer.FirstName.String, customer.LastName.String)),
		"created_at":             row.Order.CreatedAt.Time.Format("2006-01-02 15:04:05"),
		"updated_at":             row.Order.UpdatedAt.Time.Format("2006-01-02 15:04:05"),
		"sub_total":              nullableNum(row.Order.SubTotal.Int32),
		"total_fee":              nullableNum(row.Order.TotalFee.Int32),
		"campaign_code":          nullableStr(row.Order.CampaignCode.String),
		"discount":               row.Order.Discount,
		"voucher_discount":       row.Order.VoucherDiscount,
		"discount_type":          nullableStr(row.Order.DiscountType.String),
		"total_tax":              nullableStr(row.Order.TotalTax.String),
		"shipping_method":        nullableStr(row.Order.ShippingMethod.String),
		"shipping_total":         nullableStr(row.Order.ShippingTotal.String),
		"shipping_tax":           nullableStr(row.Order.ShippingTax.String),
		"delivery_tax":           nullableNum(row.Order.DeliveryTax.Int32),
		"tax_id":                 nullableNum(row.Order.TaxID.Int32),
		"tax_code":               nullableStr(row.DeliveryTaxCode.String),
		"tax_rate":               nullableStr(row.DeliveryTaxRate.String),
		"payment_method":         nullableStr(row.Order.PaymentMethod.String),
		"payment_mode":           nullableStr(row.Order.PaymentMode.String),
		"brand_id":               nullableNum(webshopBrand.ID),
		"brand_name":             nullableStr(webshopBrand.BrandName),
		"tip":                    row.Order.Tip,
		"table_order_method_id":  nullableNum(row.Order.TableOrderMethodID.Int32),
		"device_platform":        nullableStr(row.Order.DevicePlatform.String),
		"display_order_id":       nullableStr(row.Order.DisplayOrderID.String),
		"tenant_customer_id":     nullableNum(customer.ID),
		"commission":             nullableStr(commission.Commission),
		"has_transferred":        0,
		"transferred_amount":     0,
		"customer_email":         nullableStr(customer.Email.String),
		"table_order_method":     nullableNum(row.Order.TableOrderMethodID.Int32),
	}
}

func newOrderItemsData(orderItems []*db.GetOrderItemsByOrderIDRow) []serializedContent {
	items := make([]serializedContent, 0, len(orderItems))
	for _, item := range orderItems {
		items = append(items, newOrderItemData(item))
	}
	return items
}

func newOrderItemData(item *db.GetOrderItemsByOrderIDRow) serializedContent {
	return serializedContent{
		"id":             int32(item.ID),
		"order_id":       item.OrderID,
		"item_id":        item.ItemID,
		"item_name":      sanitizeString(removeEmoji(item.ItemName)),
		"quantity":       item.Quantity,
		"price_per_item": item.PricePerItem,
		"discount":       item.DiscountAmount,
		"status":         item.Status,
		"modifiers":      "",
		"created_at":     item.CreatedAt.Time.Format("2006-01-02 15:04:05"),
		"updated_at":     item.UpdatedAt.Time.Format("2006-01-02 15:04:05"),
	}
}

func sanitizeString(s string) string {
	// Quick check for ASCII-only strings (0x20–0x7E)
	asciiOnly, _ := regexp.MatchString(`^[\x20-\x7E]*$`, s)
	if asciiOnly {
		trimmed := strings.TrimSpace(s)
		return trimmed
	}

	// Ensure valid UTF-8
	if !utf8.ValidString(s) {
		s = strings.ToValidUTF8(s, "")
	}

	// Remove control characters (0x00–0x1F and 0x7F)
	ctrlRegex := regexp.MustCompile(`[\x00-\x1F\x7F]`)
	s = ctrlRegex.ReplaceAllString(s, "")

	// If contains non-ASCII chars, base64 encode
	nonASCII, _ := regexp.MatchString(`[\x80-\xFF]`, s)
	if nonASCII {
		encoded := "b64:" + base64.StdEncoding.EncodeToString([]byte(s))
		return encoded
	}

	trimmed := strings.TrimSpace(s)
	return trimmed
}

func removeEmoji(s string) string {
	re := regexp.MustCompile(`[^A-Za-z0-9 ]`)
	return re.ReplaceAllString(s, "")
}

func newOrderShopFeesData(orderShopFees []*db.GetShopFeesByOrderIDRow) []serializedContent {
	fees := make([]serializedContent, 0, len(orderShopFees))
	for _, fee := range orderShopFees {
		fees = append(fees, newOrderShopFeeData(fee))
	}
	return fees
}

func newOrderShopFeeData(fee *db.GetShopFeesByOrderIDRow) serializedContent {
	return serializedContent{
		"id":           fee.ID,
		"order_id":     fee.OrderID,
		"shop_fee_id":  fee.ShopFeeID,
		"fee_name":     fee.FeeName,
		"fee":          fee.Amount,
		"fee_type":     fee.FeeType,
		"shop_fee_tax": nullableNum(fee.ShopFeeTax.Int32),
		"tax_id":       nullableNum(fee.TaxID.Int32),
		"tax_code":     nullableStr(fee.TaxCode.String),
		"tax_rate":     nullableStr(fee.TaxRate.String),
		"created_at":   fee.CreatedAt.Time.Format("2006-01-02 15:04:05"),
		"updated_at":   fee.UpdatedAt.Time.Format("2006-01-02 15:04:05"),
	}
}

func newOrderTaxesData(orderTaxes []*db.OrderTax) []serializedContent {
	taxes := make([]serializedContent, 0, len(orderTaxes))
	for _, tax := range orderTaxes {
		taxes = append(taxes, newOrderTaxData(tax))
	}
	return taxes
}

func newOrderTaxData(tax *db.OrderTax) serializedContent {
	return serializedContent{
		"id":             tax.ID,
		"order_id":       tax.OrderID,
		"tax_rate":       tax.TaxRate,
		"tax_code":       tax.TaxCode,
		"tax_amount":     tax.TaxAmount,
		"taxable_amount": tax.TaxableAmount,
		"created_at":     tax.CreatedAt.Time.Format("2006-01-02 15:04:05"),
		"updated_at":     tax.UpdatedAt.Time.Format("2006-01-02 15:04:05"),
	}
}

func newOrderItemTaxesData(orderItemTaxes []*db.GetOrderItemTaxesRow) []serializedContent {
	taxes := make([]serializedContent, 0, len(orderItemTaxes))
	for _, itemTax := range orderItemTaxes {
		taxes = append(taxes, newOrderItemTaxData(itemTax))
	}
	return taxes
}

func newOrderItemTaxData(tax *db.GetOrderItemTaxesRow) serializedContent {
	return serializedContent{
		"id":                     tax.ID,
		"order_id":               tax.OrderID,
		"order_item_id":          tax.OrderItemID,
		"order_item_modifier_id": nullableNum(tax.OrderItemModifierID.Int32),
		"item_price":             tax.ItemPrice,
		"tax_id":                 tax.TaxID,
		"tax_profile_id":         tax.TaxProfileID,
		"tax_rule_id":            tax.TaxRuleID,
		"tax_code":               tax.TaxCode,
		"tax_rate":               tax.TaxRate.String,
		"amount":                 tax.Amount,
		"created_at":             tax.CreatedAt.Time.Format("2006-01-02 15:04:05"),
		"updated_at":             tax.UpdatedAt.Time.Format("2006-01-02 15:04:05"),
	}
}

func (o *orderSqsWrapper) ServiceBypassed() bool {
	return o.bypass
}

func (*orderSqsWrapper) shouldSentToQueue(s string) bool {
	return s == "CANCELED" || s == "COMPLETED"
}
