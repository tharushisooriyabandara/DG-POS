package types

import "encoding/json"

type RunMigrationsRequest struct {
	Step       int `query:"step"`
	TenantCode string

	RunForAllTenants bool
}

/*
	types for authentication
*/

type LoginRequest struct {
	OutletCode string `json:"outlet_code" validate:"required"`
	BrandID    int32  `json:"brand_id" validate:"required"`
	Email      string `json:"email" validate:"required,email"`
	Pin        string `json:"pin" validate:"required,numeric,min=4,max=6"`
}

type RefreshRequest struct {
	RefreshToken string `json:"refreshToken" validate:"required,jwt"`
}

type VerifyPinRequest struct {
	OutletCode string `json:"outlet_code" validate:"required"`
	Email      string `json:"email" validate:"required,email"`
	Pin        string `json:"pin" validate:"required,numeric,min=4,max=6"`
}

/**************************/

/*
types for orders
*/

type UpdateOrderStatusRequest struct {
	Requestor SessionUser

	OrderID uint64
	Status  string `json:"status" validate:"required,oneof=QUEUE PREPARING READY SERVED DELIVERED COMPLETED CANCELLED"`
	Note    string `json:"note"`
}

type CreateOrderRequest struct {
	Requestor SessionUser

	CustomerID             int32           `json:"customer_id" validate:"required_with=CustomerName"`
	CustomerName           string          `json:"customer_name" validate:"required_with=CustomerID"`
	DeliveryDateTime       string          `json:"delivery_date_time" validate:"required,datetime=2006-01-02 15:04:05"`
	DeliveryPlatformID     int32           `json:"platform_id" validate:"required"`
	Discount               float64         `json:"discount" validate:"gte=0"`
	DiscountModeApplied    string          `json:"discount_mode_applied"`
	DiscountPercentage     float64         `json:"discount_percentage"`
	DiscountType           string          `json:"discount_type"`
	DisplayOrderID         string          `json:"display_order_id" validate:"required"`
	OrderNote              string          `json:"order_note"`
	ShippingMethod         string          `json:"shipping_method" validate:"required,oneof=TAKEAWAY DELIVERY DINE-IN"`
	ShippingTax            float64         `json:"shipping_tax" validate:"gte=0"`
	ShippingTotal          float64         `json:"shipping_total" validate:"gte=0"`
	DeliveryTax            DeliveryTax     `json:"delivery_tax"`
	ShopID                 int32           `json:"shop_id" validate:"required"`
	SubTotal               float64         `json:"sub_total" validate:"required"`
	TableID                int             `json:"table_id" validate:"required_if=ShippingMethod DINE-IN"`
	Tip                    float64         `json:"tip" validate:"gte=0"`
	TipPercentage          float64         `json:"tip_percentage" validate:"gte=0"`
	TotalAmount            float64         `json:"total_amount" validate:"required"`
	TotalTax               float64         `json:"total_tax" validate:"gte=0"`
	TotalFee               float64         `json:"total_fee" validate:"gte=0"`
	UserID                 int32           `json:"user_id" validate:"required"`
	OrderReceiverAddressID int32           `json:"order_receiver_address_id" validate:"required_if=ShippingMethod DELIVERY"`
	OrderTaxes             []*OrderTax     `json:"order_taxes"`
	OrderShopFees          []*OrderShopFee `json:"order_shop_fees" validate:"dive"`
	Vouchers               []*OrderVoucher `json:"vouchers" validate:"dive"`
	OrderItems             []*OrderItem    `json:"order_items" validate:"required,gte=1,dive"`
	OrderPayments          []*OrderPayment `json:"payments" validate:"dive"`
}

type OrderTax struct {
	Tax_rate       float64 `json:"tax_rate"`
	Tax_code       string  `json:"tax_code"`
	Tax_amount     float64 `json:"tax_amount"`
	Taxable_amount float64 `json:"taxable_amount"`
}

type DeliveryTax struct {
	TaxID     int32   `json:"tax_id"`
	TaxAmount float64 `json:"tax_amount"`
}

type OrderShopFee struct {
	ShopFeeId int     `json:"shop_fee_id" validate:"required"`
	Amount    float64 `json:"amount" validate:"required"`
	TaxID     int32   `json:"tax_id"`
	TaxAmount float64 `json:"tax_amount"`
}

type OrderVoucher struct {
	VoucherCode     string   `json:"voucher_code" validate:"required"`
	VoucherDiscount float64  `json:"voucher_discount" validate:"required"`
	VoucherValue    string   `json:"voucher_value" validate:"required"`
	ValueType       string   `json:"value_type" validate:"required"`
	PurchaseType    string   `json:"purchase_type"`
	PaymentType     string   `json:"payment_type"`
	ValidCategories []string `json:"valid_categories"`
	Validation      []string `json:"validation"`
}

type OrderItem struct {
	DiscountAmount  float64            `json:"discount_amount" validate:"gte=0"`
	IsSale          bool               `json:"is_sale"`
	ItemID          int                `json:"item_id" validate:"required"`
	ItemName        string             `json:"item_name" validate:"required"`
	ModifierDetails []*ModifierDetails `json:"modifier_details" validate:"dive"`
	Note            string             `json:"note"`
	OriginalPrice   float64            `json:"original_price" validate:"gte=0"`
	PricePerItem    float64            `json:"price_per_item" validate:"required"`
	Quantity        int                `json:"quantity" validate:"required"`
	Tax             float64            `json:"tax" validate:"gte=0"`
	Total           float64            `json:"total" validate:"required"`
	TaxDetails      *TaxDetails        `json:"tax_details"`
}

type ModifierDetails struct {
	ModifierGroupName string            `json:"modifier_main" validate:"required"`
	ModifierMainItem  int               `json:"modifier_main_item" validate:"required"`
	Quantity          int               `json:"quantity" validate:"required"`
	ModifierItem      *ModifierItem     `json:"modifier_item" validate:"required"`
	Modifiers         []*NestedModifier `json:"modifiers" validate:"dive"`
}

type ModifierItem struct {
	ExternalItemID string      `json:"external_item_id" validate:"required"`
	ItemName       string      `json:"item_name" validate:"required"`
	Price          float64     `json:"price" validate:"gte=0"`
	OriginalPrice  float64     `json:"original_price" validate:"gte=0"`
	TaxDetails     *TaxDetails `json:"tax_details"`
}

type NestedModifier struct {
	ModifierMain struct {
		Id       int    `json:"id" validate:"required"`
		Title    string `json:"title" validate:"required"`
		Quantity int    `json:"quantity" validate:"required"`
	} `json:"modifier_main" validate:"required"`
	ModifierItem *ModifierItem `json:"modifier_item" validate:"required"`
}

type TaxDetails struct {
	TaxProfileID int32   `json:"tax_profile_id"`
	TaxRuleID    int32   `json:"tax_rule_id"`
	TaxID        int32   `json:"tax_id"`
	TaxCode      string  `json:"tax_code"`
	TaxRate      float64 `json:"tax_rate"`
	Amount       float64 `json:"amount"`
}

type OrderPayment struct {
	PayingAmount  float64 `json:"paying_amount" validate:"required"`
	PaymentMethod string  `json:"payment_method" validate:"required,oneof=CASH CARD"`
	Cash          float64 `json:"cash" validate:"required_if=PaymentMethod CASH,omitempty,gtefield=PayingAmount"`
	Balance       float64 `json:"balance" validate:"gte=0"`
	TransactionID string  `json:"transaction_id" validate:"required_if=PaymentMethod CARD"`
}

type UpdateOrderRequest struct {
	Requestor SessionUser

	CustomerID          int32           `json:"customer_id" validate:"required_with=CustomerName"`
	CustomerName        string          `json:"customer_name" validate:"required_with=CustomerID"`
	Discount            float64         `json:"discount" validate:"gte=0"`
	DiscountModeApplied string          `json:"discount_mode_applied"`
	DiscountPercentage  float64         `json:"discount_percentage"`
	DiscountType        string          `json:"discount_type"`
	OrderNote           string          `json:"order_note"`
	OrderItems          []*OrderItem    `json:"order_items" validate:"required,gte=1,dive"`
	ShippingMethod      string          `json:"shipping_method" validate:"required,oneof=TAKEAWAY DELIVERY DINE-IN"`
	ShippingTax         float64         `json:"shipping_tax" validate:"gte=0"`
	ShippingTotal       float64         `json:"shipping_total" validate:"gte=0"`
	SubTotal            float64         `json:"sub_total" validate:"required"`
	TableID             int             `json:"table_id" validate:"required_if=ShippingMethod DINE-IN"`
	Tip                 float64         `json:"tip" validate:"gte=0"`
	TipPercentage       float64         `json:"tip_percentage" validate:"gte=0"`
	TotalAmount         float64         `json:"total_amount" validate:"required"`
	TotalTax            float64         `json:"total_tax" validate:"gte=0"`
	TotalFee            float64         `json:"total_fee" validate:"gte=0"`
	UserID              int32           `json:"user_id" validate:"required"`
	Vouchers            []*OrderVoucher `json:"vouchers" validate:"dive"`
	OrderTaxes          []*OrderTax     `json:"order_taxes"`
	OrderShopFees       []*OrderShopFee `json:"order_shop_fees" validate:"dive"`
}

type CreateOrderPaymentRequest struct {
	Requestor SessionUser

	OrderID  uint64
	Payments []*OrderPayment `json:"payments" validate:"required,gte=1,dive"`
}

type UpdateOrderUserIDRequest struct {
	OrderID uint64
	UserID  int32 `json:"user_id" validate:"required"`
}

type RefundOrderRequest struct {
	Requestor SessionUser

	OrderID           uint64
	SaleTransactionID int64   `json:"sale_transaction_id"`
	RefundMode        string  `json:"refund_mode" validate:"required,oneof=CASH CARD"`
	RefundAmount      float64 `json:"refund_amount" validate:"required"`
	Reason            string  `json:"refund_reason" validate:"required"`
}

// type OrderRefundsRequest struct {
// 	Requestor SessionUser

// 	OrderID    uint64            `json:"order_id" validate:"required"`
// 	RefundMode string            `json:"refund_mode" validate:"required,oneof=CASH CARD"`
// 	Reason     string            `json:"refund_reason" validate:"required"`
// 	Refunds    []*SaleRefundData `json:"refunds" validate:"required,gte=1,dive"`
// }

// type SaleRefundData struct {
// 	SaleTransactionID int64   `json:"sale_transaction_id" validate:"required"`
// 	RefundAmount      float64 `json:"refund_amount" validate:"required"`
// }

/**************************/

type CreateCustomerRequest struct {
	Requestor SessionUser

	Name        string `json:"name" validate:"required"`
	CountryCode string `json:"country_code" validate:"required"`
	PhoneNumber string `json:"phone_number" validate:"required,numeric,min=9"`
}

type CreateCustomerAddressRequest struct {
	CustomerID     int32
	Label          string `json:"label" validate:"required"`
	FlatNo         string `json:"flat_no"`
	HouseNo        string `json:"house_no"`
	AddressLine1   string `json:"address_line_1" validate:"required"`
	AddressLine2   string `json:"address_line_2"`
	Latitude       string `json:"latitude"`
	Longitude      string `json:"longitude"`
	City           string `json:"city"`
	Landmark       string `json:"landmark"`
	PostalCode     string `json:"postal_code"`
	DefaultAddress bool   `json:"default_address"`
}

type UpdateCustomerRequest struct {
	Requestor SessionUser

	ID          uint64
	FirstName   string                          `json:"first_name"`
	LastName    string                          `json:"last_name"`
	CountryCode string                          `json:"country_code"`
	Phone       string                          `json:"phone"`
	Addresses   []*UpdateCustomerAddressRequest `json:"addresses"`
}

type UpdateCustomerAddressRequest struct {
	ID             int64  `json:"id" validate:"required"`
	Label          string `json:"label" validate:"required"`
	FlatNo         string `json:"flat_no"`
	HouseNo        string `json:"house_no"`
	AddressLine1   string `json:"address_line_1" validate:"required"`
	AddressLine2   string `json:"address_line_2"`
	Latitude       string `json:"latitude"`
	Longitude      string `json:"longitude"`
	City           string `json:"city"`
	Landmark       string `json:"landmark"`
	PostalCode     string `json:"postal_code"`
	DefaultAddress bool   `json:"default_address"`
}

type GetOrdersRequest struct {
	OutletID    int32   `query:"outlet_id"`
	PlatformIds []int32 `query:"platforms"`
	Status      string  `query:"status" validate:"omitempty,oneof=CREATED ACCEPTED READY_FOR_PICKUP MISSED DECLINED CANCELLED QUEUE PREPARING READY SERVED DELIVERED COMPLETED"`
	FromDate    string  `query:"from_date" validate:"omitempty,datetime=2006-01-02 15:04:05"`
	ToDate      string  `query:"to_date" validate:"omitempty,datetime=2006-01-02 15:04:05"`
	SortBy      string  `query:"sort" validate:"omitempty,oneof=asc desc"`
}

type LogActivityRequest struct {
	Requestor SessionUser

	Event       string `json:"event" validate:"required"`
	Subject     string `json:"subject" validate:"required"`
	SubjectId   uint64 `json:"subject_id" validate:"required"`
	Description string `json:"description"`
}

type QueryFilteredRequest struct {
	OutletID int64 `query:"outlet_id"`
	BrandID  int64 `query:"brand_id"`
}

type GetShiftInfoRequest struct {
	Requestor SessionUser

	UserID   int64
	FromDate string `query:"from" validate:"required,datetime=2006-01-02 15:04:05"`
	ToDate   string `query:"to" validate:"omitempty,datetime=2006-01-02 15:04:05"`
}

type GetShopShiftInfoRequest struct {
	ShopID   uint32
	FromDate string `query:"from" validate:"required,datetime=2006-01-02 15:04:05"`
	ToDate   string `query:"to" validate:"omitempty,datetime=2006-01-02 15:04:05"`
}

type GetUsersRequest struct {
	OutletCode string   `query:"outlet-code" validate:"required"`
	Roles      []uint32 `query:"roles"`
}

type ChangePinRequest struct {
	UserID int32
	NewPin string `json:"new_pin" validate:"required,numeric,min=4,max=6"`
}

type UpdateTableStatusRequest struct {
	TableID        uint64 `json:"table_id" validate:"required"`
	Status         string `json:"status" validate:"required,oneof=AVAILABLE RESERVED"`
	OngoingOrderID uint64 `json:"ongoing_order_id" validate:"required_if=Status RESERVED"`
}

type CreateCashDrawerRequest struct {
	Requestor SessionUser
}

type OpenCashDrawerSessionRequest struct {
	Requestor SessionUser

	OpeningBalance float64 `json:"opening_balance" validate:"gte=0"`
}

type CloseCashDrawerSessionRequest struct {
	Requestor SessionUser

	ClosingBalanceCounted float64 `json:"counted_balance" validate:"gte=0"`
}

type RecordCashMovementRequest struct {
	Requestor    SessionUser
	IsAPIRequest bool

	MovementType string  `json:"movement_type" validate:"required,oneof=PAY_IN PAY_OUT OTHER_SALES OTHER_REFUND CARD_SALE_CASH_REFUND"`
	Note         string  `json:"note"`
	Amount       float64 `json:"amount" validate:"required"`
}

type GetCashDrawerSessionsRequest struct {
	Requestor SessionUser

	From string `query:"from" validate:"required,datetime=2006-01-02 15:04:05"`
	To   string `query:"to" validate:"omitempty,datetime=2006-01-02 15:04:05"`
}

type GetActiveCashDrawerSessionInfoRequest struct {
	Requestor    SessionUser
	IsForXReport bool `query:"x_report"`
}

type GetCashDrawerTransactionHistoryRequest struct {
	Requestor SessionUser

	From string `query:"from" validate:"required,datetime=2006-01-02 15:04:05"`
	To   string `query:"to" validate:"omitempty,datetime=2006-01-02 15:04:05"`
}

type GetShopConfigRequest struct {
	ShopID     int32  `params:"shopId"`
	BrandID    int32  `query:"brand"`
	TerminalID int32  `query:"terminal"`
	ConfigType string `params:"configType" validate:"required,oneof=menu order floor_plan"`
}

type UpdateShopConfigRequest struct {
	ShopID     int32           `params:"shopId"`
	BrandID    int32           `query:"brand"`
	TerminalID int32           `query:"terminal"`
	Data       json.RawMessage `json:"data"`
	ConfigType string          `params:"configType" validate:"required,oneof=menu order floor_plan"`
}

type DgPosTmpPaymentRequest struct {
	TypeId        string  `json:"type_id"`
	Type          string  `json:"type"`
	PaymentMode   string  `json:"payment_mode"`
	PaymentAmount float64 `json:"payment_amount"`
	TransactionID string  `json:"transaction_id"`
}
