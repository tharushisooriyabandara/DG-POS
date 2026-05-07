package types

import (
	"encoding/json"
	"time"
)

type GetUsersResponse struct {
	ID        uint64 `json:"id"`
	FirstName string `json:"first_name"`
	LastName  string `json:"last_name"`
	ContactNo string `json:"contact_no"`
	Email     string `json:"email"`
	Address   string `json:"address"`
	RoleId    string `json:"role_id"`
	Role      string `json:"role"`
}

type GetUserResponse struct {
	ID                 uint64    `json:"id"`
	FirstName          string    `json:"first_name"`
	LastName           string    `json:"last_name"`
	Email              string    `json:"email"`
	Address            string    `json:"address"`
	ContactNo          string    `json:"contact_no"`
	Status             string    `json:"status"`
	RoleId             string    `json:"role_id"`
	Role               string    `json:"role"`
	ReportServiceToken string    `json:"report_service_token"`
	CreatedAt          time.Time `json:"created_at"`
	UpdatedAt          time.Time `json:"updated_at"`
}

type GetCustomersResponse struct {
	ID          uint64             `json:"id"`
	FirstName   string             `json:"first_name"`
	LastName    string             `json:"last_name"`
	CountryCode string             `json:"country_code"`
	Phone       string             `json:"phone"`
	Addresses   []*CustomerAddress `json:"addresses"`
}

type GetCustomerDetailsResponse struct {
	*GetCustomersResponse
	Orders []*GetOrdersResponse `json:"orders"`
}

type CustomerAddress struct {
	ID             int64  `json:"id"`
	Label          string `json:"label"`
	FlatNo         string `json:"flat_no"`
	HouseNo        string `json:"house_no"`
	AddressLine1   string `json:"address_line_1"`
	AddressLine2   string `json:"address_line_2"`
	Latitude       string `json:"latitude"`
	Longitude      string `json:"longitude"`
	City           string `json:"city"`
	Landmark       string `json:"landmark"`
	PostalCode     string `json:"postal_code"`
	DefaultAddress bool   `json:"default_address"`
}

type GetShopResponse struct {
	ID                           uint64               `json:"id"`
	Name                         string               `json:"name"`
	ShopLogo                     string               `json:"shop_logo"`
	FranchiseID                  int32                `json:"franchise_id"`
	Code                         string               `json:"code"`
	Email                        string               `json:"email"`
	Address                      string               `json:"address"`
	ContactNo                    string               `json:"contact_no"`
	BusinessRegNo                string               `json:"business_reg_no"`
	Status                       string               `json:"status"`
	OrderStatus                  string               `json:"order_status"`
	LastUpdatedMenu              int32                `json:"last_updated_menu"`
	ServiceAvailability          string               `json:"service_availability"`
	MinimumAmountForFreeDelivery float64              `json:"minimum_amount_for_free_delivery"`
	MinimumAmountForDelivery     float64              `json:"minimum_amount_for_delivery"`
	OneTimePromotionValue        float64              `json:"one_time_promotion_value"`
	OneTimePromotionSpendAmount  float64              `json:"one_time_promotion_spend_amount"`
	Latitude                     string               `json:"latitude"`
	Longitude                    string               `json:"longitude"`
	GoogleLocationUrl            string               `json:"google_location_url"`
	OneTimePromotionType         string               `json:"one_time_promotion_type"`
	MaximumPromotionValue        float64              `json:"maximum_promotion_value"`
	SelectedMenu                 int32                `json:"selected_menu"`
	IsDefault                    bool                 `json:"is_default"`
	CountryCode                  string               `json:"country_code"`
	Timezone                     string               `json:"timezone"`
	Currency                     string               `json:"currency"`
	CurrencyCode                 string               `json:"currency_code"`
	DeliveryPlatformEnable       bool                 `json:"delivery_platform_enable"`
	HasCashPayment               bool                 `json:"has_cash_payment"`
	HasCardPayment               bool                 `json:"has_card_payment"`
	DelivergateAccount           bool                 `json:"delivergate_account"`
	TaxMode                      string               `json:"tax_mode"`
	TaxRegNo                     string               `json:"tax_reg_no"`
	CreatedAt                    time.Time            `json:"created_at"`
	UpdatedAt                    time.Time            `json:"updated_at"`
	Dp                           *GetDpResponse       `json:"delivery_platform"`
	ShopFees                     []*ShopFee           `json:"shop_fees"`
	TaxProfiles                  []*TaxProfile        `json:"tax_profiles"`
	PrinterGroups                []*PrinterGroupsResp `json:"printer_groups"`
}

type PrinterGroupsResp struct {
	ID          uint64    `json:"id"`
	Name        string    `json:"name"`
	Description string    `json:"description"`
	Status      bool      `json:"status"`
	CreatedAt   time.Time `json:"created_at"`
	UpdatedAt   time.Time `json:"updated_at"`
}

type ShopFee struct {
	ID         uint64              `json:"id"`
	Type       string              `json:"type"`
	FeeType    string              `json:"fee_type"`
	FeeName    string              `json:"fee_name"`
	Fee        float64             `json:"fee"`
	Mandatory  bool                `json:"mandatory"`
	TaxDetails *TaxDetailsResponse `json:"tax_details"`
}

type TaxProfile struct {
	ID          uint64     `json:"id"`
	Name        string     `json:"name"`
	Description string     `json:"description"`
	Status      bool       `json:"status"`
	CreatedAt   time.Time  `json:"created_at"`
	UpdatedAt   time.Time  `json:"updated_at"`
	TaxRules    []*TaxRule `json:"tax_rules"`
}

type TaxRule struct {
	ID                uint64              `json:"id"`
	TaxProfileID      uint64              `json:"tax_profile_id"`
	TaxID             uint64              `json:"tax_id"`
	Name              string              `json:"name"`
	CreatedAt         time.Time           `json:"created_at"`
	UpdatedAt         time.Time           `json:"updated_at"`
	Tax               *Tax                `json:"tax"`
	TaxRuleConditions []*TaxRuleCondition `json:"tax_rule_conditions"`
}

type Tax struct {
	ID          uint64    `json:"id"`
	Name        string    `json:"name"`
	Code        string    `json:"code"`
	Description string    `json:"description"`
	Rate        float64   `json:"rate"`
	Status      bool      `json:"status"`
	CreatedAt   time.Time `json:"created_at"`
	UpdatedAt   time.Time `json:"updated_at"`
}

type TaxRuleCondition struct {
	ID             uint64    `json:"id"`
	TaxRuleID      uint64    `json:"tax_rule_id"`
	ConditionType  string    `json:"condition_type"`
	ConditionValue string    `json:"condition_value"`
	MinValue       float64   `json:"min_value"`
	MaxValue       float64   `json:"max_value"`
	StartDate      time.Time `json:"start_date"`
	EndDate        time.Time `json:"end_date"`
	CreatedAt      time.Time `json:"created_at"`
	UpdatedAt      time.Time `json:"updated_at"`
}

type GetDpResponse struct {
	ID                 uint64    `json:"id"`
	PlatformID         int32     `json:"platform_id"`
	PlatformName       string    `json:"platform_name"`
	Name               string    `json:"name"`
	WebshopBrandID     int32     `json:"webshop_brand_id"`
	WebshopBrandName   string    `json:"webshop_brand_name"`
	ApiUrl             string    `json:"api_url"`
	AuthUrl            string    `json:"auth_url"`
	ApiParameters      string    `json:"api_parameters"`
	Logo               string    `json:"logo"`
	Status             string    `json:"status"`
	OutletCode         string    `json:"outlet_code"`
	SiteID             string    `json:"site_id"`
	BranchID           string    `json:"branch_id"`
	AccessToken        string    `json:"access_token"`
	PrimaryColor       string    `json:"primary_color"`
	CanUpload          bool      `json:"can_upload"`
	AutoAccepting      bool      `json:"auto_accepting"`
	FranchiseID        int32     `json:"franchise_id"`
	OutletID           int64     `json:"outlet_id"`
	MenuID             string    `json:"menu_id"`
	MenuUploadStatus   string    `json:"menu_upload_status"`
	StoreStatus        string    `json:"store_status"`
	AvailableFrom      time.Time `json:"available_from"`
	TenderTypes        string    `json:"tender_types"`
	IsMaster           bool      `json:"is_master"`
	ParentPlatform     int32     `json:"parent_platform"`
	PrepTime           int32     `json:"prep_time"`
	OwnDriver          bool      `json:"own_driver"`
	MenuPublishedAt    time.Time `json:"menu_published_at"`
	WebshopSetupStatus bool      `json:"webshop_setup_status"`
	HasCashPayment     bool      `json:"has_cash_payment"`
	HasCardPayment     bool      `json:"has_card_payment"`
	SelectedMenu       int32     `json:"selected_menu"`
	DeletedAt          time.Time `json:"deleted_at"`
	CreatedAt          time.Time `json:"created_at"`
	UpdatedAt          time.Time `json:"updated_at"`
}

/*
	types for authentication
*/

// LoginResponse is the response format for the login endpoint
type LoginResponse struct {
	AccessToken  string `json:"accessToken"`
	RefreshToken string `json:"refreshToken"`
}

/***********************************************/

type GetOrdersResponse struct {
	ID                   uint64    `json:"id"`
	ShopID               int32     `json:"shop_id"`
	DeliveryPlatformID   int32     `json:"delivery_platform_id"`
	DeliveryPlatformName string    `json:"delivery_platform_name"`
	PlatformID           int32     `json:"platform_id"`
	PlatformName         string    `json:"platform_name"`
	PlatformLogo         string    `json:"platform_logo"`
	RemoteOrderID        string    `json:"remote_order_id"`
	DisplayOrderID       string    `json:"display_order_id"`
	DeliveryDateTime     time.Time `json:"delivery_date_time"`
	TotalAmount          float64   `json:"total_amount"`
	PaymentStatus        string    `json:"payment_status"`
	SubTotal             float64   `json:"sub_total"`
	TotalFee             float64   `json:"total_fee"`
	CampaignCode         string    `json:"campaign_code"`
	Discount             float64   `json:"discount"`
	DiscountModeApplied  string    `json:"discount_mode_applied"`
	DiscountPercentage   float64   `json:"discount_percentage"`
	DiscountType         string    `json:"discount_type"`
	Vouchers             string    `json:"vouchers"`
	Status               string    `json:"status"`
	CancelledReason      string    `json:"cancelled_reason"`
	OrderTypeID          int32     `json:"order_type_id"`
	Note                 string    `json:"note"`
	CreatedAt            time.Time `json:"created_at"`
	UpdatedAt            time.Time `json:"updated_at"`
	CustomerID           int32     `json:"customer_id"`
	CustomerName         string    `json:"customer_name"`
	UserID               int32     `json:"user_id"`
	DeliveryLocationID   int32     `json:"delivery_location_id"`
	ShippingMethod       string    `json:"shipping_method"`
	ShippingTotal        float64   `json:"shipping_total"`
	ShippingTax          float64   `json:"shipping_tax"`
	TotalTax             float64   `json:"total_tax"`
	CashDue              string    `json:"cash_due"`
	Surcharge            float64   `json:"surcharge"`
	ContactAccessCode    string    `json:"contact_access_code"`
	TestingOrder         bool      `json:"testing_order"`
	CancelledByCustomer  bool      `json:"cancelled_by_customer"`
	PaymentMethod        string    `json:"payment_method"`
	PaymentMode          string    `json:"payment_mode"`
	IsScheduled          bool      `json:"is_scheduled"`
	IsTableOrder         bool      `json:"is_table_order"`
	Tip                  string    `json:"tip"`
	TipPercentage        string    `json:"tip_percentage"`
	TableID              int32     `json:"table_id"`
	TableName            string    `json:"table_name"`
	TableOrderingsID     int64     `json:"table_orderings_id"`
	TableOrderMethodID   int32     `json:"table_order_method_id"`
	TableOrderMethod     string    `json:"table_order_method"`
	DevicePlatform       string    `json:"device_platform"`
	OrderDelayed         bool      `json:"order_delayed"`
	UniqueOrderID        string    `json:"unique_order_id"`
	CashDrawerSessionID  int64     `json:"cash_drawer_session_id"`
	OrderSessionID       int32     `json:"order_session_id"`
}

type GetTablesResponse struct {
	ID               uint64                        `json:"id"`
	ShopID           int32                         `json:"shop_id"`
	BrandID          int32                         `json:"brand_id"`
	Name             string                        `json:"name"`
	Description      string                        `json:"description"`
	SeatCount        int32                         `json:"seat_count"`
	Status           string                        `json:"status"`
	TableOrderingsID int64                         `json:"table_orderings_id"`
	CreatedAt        time.Time                     `json:"created_at"`
	UpdatedAt        time.Time                     `json:"updated_at"`
	Order            *GetOrderResponse             `json:"order"`
	OngoingOrders    []*TableOngoingOrdersResponse `json:"ongoing_orders"`
}

type TableOngoingOrdersResponse struct {
	OrderID          uint64    `json:"order_id"`
	DisplayOrderID   string    `json:"display_order_id"`
	Status           string    `json:"status"`
	TotalAmount      float64   `json:"total_amount"`
	CustomerName     string    `json:"customer_name"`
	OrderSessionID   int32     `json:"order_session_id"`
	IsTableOrder     bool      `json:"is_table_order"`
	ShippingMethod   string    `json:"shipping_method"`
	PaymentStatus    string    `json:"payment_status"`
	TableOrderMethod string    `json:"table_order_method"`
	PlatformLogo     string    `json:"platform_logo"`
	CreatedAt        time.Time `json:"created_at"`
}

type GetOrderResponse struct {
	ID                   uint64                       `json:"id"`
	ShopID               int32                        `json:"shop_id"`
	DeliveryPlatformID   int32                        `json:"delivery_platform_id"`
	DeliveryPlatformName string                       `json:"delivery_platform_name"`
	PlatformID           int64                        `json:"platform_id"`
	PlatformName         string                       `json:"platform_name"`
	PlatformLogo         string                       `json:"platform_logo"`
	RemoteOrderID        string                       `json:"remote_order_id"`
	DisplayOrderID       string                       `json:"display_order_id"`
	DeliveryDateTime     time.Time                    `json:"delivery_date_time"`
	TotalAmount          float64                      `json:"total_amount"`
	SubTotal             float64                      `json:"sub_total"`
	TotalFee             float64                      `json:"total_fee"`
	CampaignCode         string                       `json:"campaign_code"`
	Discount             float64                      `json:"discount"`
	VoucherDiscount      float64                      `json:"voucher_discount"`
	DiscountPercentage   float64                      `json:"discount_percentage"`
	DiscountModeApplied  string                       `json:"discount_mode_applied"`
	DiscountType         string                       `json:"discount_type"`
	Vouchers             []OrderVoucherResponse       `json:"vouchers"`
	Status               string                       `json:"status"`
	CancelledReason      string                       `json:"cancelled_reason"`
	OrderTypeID          int32                        `json:"order_type_id"`
	Note                 string                       `json:"note"`
	CreatedAt            time.Time                    `json:"created_at"`
	UpdatedAt            time.Time                    `json:"updated_at"`
	CustomerID           int32                        `json:"customer_id"`
	CustomerName         string                       `json:"customer_name"`
	UserID               int32                        `json:"user_id"`
	DeliveryLocationID   int32                        `json:"delivery_location_id"`
	ShippingMethod       string                       `json:"shipping_method"`
	ShippingTotal        float64                      `json:"shipping_total"`
	ShippingTax          float64                      `json:"shipping_tax"`
	DeliveryTax          float64                      `json:"delivery_tax"`
	TaxID                int32                        `json:"tax_id"`
	TaxRate              string                       `json:"tax_rate"`
	TaxCode              string                       `json:"tax_code"`
	TotalTax             float64                      `json:"total_tax"`
	CashDue              string                       `json:"cash_due"`
	Surcharge            float64                      `json:"surcharge"`
	ContactAccessCode    string                       `json:"contact_access_code"`
	TestingOrder         bool                         `json:"testing_order"`
	CancelledByCustomer  bool                         `json:"cancelled_by_customer"`
	PaymentMethod        string                       `json:"payment_method"`
	PaymentMode          string                       `json:"payment_mode"`
	PaymentStatus        string                       `json:"payment_status"`
	RefundStatus         string                       `json:"refund_status"`
	PaymentType          string                       `json:"payment_type"`
	IsScheduled          bool                         `json:"is_scheduled"`
	IsTableOrder         bool                         `json:"is_table_order"`
	Tip                  string                       `json:"tip"`
	TipPercentage        string                       `json:"tip_percentage"`
	TableID              int32                        `json:"table_id"`
	TableOrderingsID     int32                        `json:"table_orderings_id"`
	TableName            string                       `json:"table_name"`
	TableOrderMethodID   int32                        `json:"table_order_method_id"`
	TableOrderMethod     string                       `json:"table_order_method"`
	DevicePlatform       string                       `json:"device_platform"`
	OrderDelayed         bool                         `json:"order_delayed"`
	UniqueOrderID        string                       `json:"unique_order_id"`
	UserShiftID          int32                        `json:"user_shift_id"`
	OrderSessionID       int32                        `json:"order_session_id"`
	SessionPaymentType   string                       `json:"session_payment_type"`
	RefundBalance        float64                      `json:"refund_balance"`
	RefundBalances       *RefundBalanceResponse       `json:"refund_balances"`
	OrderTaxes           []*GetOrderTaxesResponse     `json:"order_taxes"`
	Transactions         []*OrderTransactionsResponse `json:"transactions"`
	DelivergateCustomer  *GetCustomersResponse        `json:"delivergate_customer"`
	ShippingDetails      *ShippingDetailsResponse     `json:"shipping_details"`
	ShopFees             []*ShopFeeResponse           `json:"shop_fees"`
	Items                []*GetOrderItemsResponse     `json:"items"`
}

type RefundBalanceResponse struct {
	TotalRefundBalance float64 `json:"total_refund_balance"`
	CashRefundBalance  float64 `json:"cash_refund_balance"`
	CardRefundBalance  float64 `json:"card_refund_balance"`
}

type OrderTransactionsResponse struct {
	TransactionType   string    `json:"transaction_type"`
	TransactionAmount float64   `json:"transaction_amount"`
	TransactionMode   string    `json:"transaction_mode"`
	CreatedAt         time.Time `json:"created_at"`
	UpdatedAt         time.Time `json:"updated_at"`
	Reason            string    `json:"reason"`
}

type OrderVoucherResponse struct {
	VoucherCode     string  `json:"voucher_code"`
	VoucherDiscount float64 `json:"voucher_discount"`
	VoucherValue    string  `json:"voucher_value"`
	ValueType       string  `json:"value_type"`
}

type GetOrderTaxesResponse struct {
	TaxRate       string  `json:"tax_rate"`
	TaxCode       string  `json:"tax_code"`
	TaxAmount     float64 `json:"tax_amount"`
	TaxableAmount float64 `json:"taxable_amount"`
}

type ShopFeeResponse struct {
	ShopFeeID  int32   `json:"shop_fee_id"`
	FeeType    string  `json:"fee_type"`
	FeeName    string  `json:"fee_name"`
	Amount     float64 `json:"amount"`
	ShopFeeTax float64 `json:"shop_fee_tax"`
	TaxID      int32   `json:"tax_id"`
	TaxCode    string  `json:"tax_code"`
	TaxRate    string  `json:"tax_rate"`
}

type ShippingDetailsResponse struct {
	ID           uint64    `json:"id"`
	OrderID      int64     `json:"order_id"`
	FirstName    string    `json:"first_name"`
	LastName     string    `json:"last_name"`
	Email        string    `json:"email"`
	CountryCode  string    `json:"country_code"`
	Country      string    `json:"country"`
	Phone        string    `json:"phone"`
	FlatNo       string    `json:"flat_no"`
	HouseNo      string    `json:"house_no"`
	AddressLine1 string    `json:"address_line_1"`
	AddressLine2 string    `json:"address_line_2"`
	City         string    `json:"city"`
	State        string    `json:"state"`
	Postcode     string    `json:"postcode"`
	OrderTmpID   int64     `json:"order_tmp_id"`
	Type         string    `json:"type"`
	Landmark     string    `json:"landmark"`
	Latitude     string    `json:"latitude"`
	Longitude    string    `json:"longitude"`
	CreatedAt    time.Time `json:"created_at"`
	UpdatedAt    time.Time `json:"updated_at"`
}

type GetOrderItemsResponse struct {
	ID             uint64               `json:"id"`
	OrderID        int32                `json:"orderId"`
	ItemID         int32                `json:"itemId"`
	Quantity       float64              `json:"quantity"`
	PricePerItem   int32                `json:"pricePerItem"`
	Total          int32                `json:"total"`
	OriginalPrice  int32                `json:"originalPrice"`
	IsSale         bool                 `json:"isSale"`
	DiscountAmount int32                `json:"discountAmount"`
	Status         string               `json:"status"`
	CreatedAt      time.Time            `json:"createdAt"`
	UpdatedAt      time.Time            `json:"updatedAt"`
	ItemName       string               `json:"itemName"`
	CategoryName   string               `json:"categoryName"`
	Tax            int32                `json:"tax"`
	Note           string               `json:"note"`
	PrinterGroups  []*PrinterGroupsResp `json:"printer_groups"`
	TaxDetails     *TaxDetailsResponse  `json:"tax_details"`
	Modifiers      json.RawMessage      `json:"modifiers"`
}

type TaxDetailsResponse struct {
	OrderItemTaxID int64   `json:"order_item_tax_id,omitempty"`
	TaxProfileID   int32   `json:"tax_profile_id,omitempty"`
	TaxRuleID      int32   `json:"tax_rule_id,omitempty"`
	TaxID          int32   `json:"tax_id"`
	TaxCode        string  `json:"tax_code"`
	TaxRate        float64 `json:"tax_rate"`
	Amount         float64 `json:"amount,omitempty"`
}

type UserShiftInfoResponse struct {
	UserID           int64              `json:"user_id"`
	FromDate         time.Time          `json:"from_date"`
	ToDate           time.Time          `json:"to_date"`
	OrderCount       int64              `json:"order_count"`
	TotalCashAmount  float64            `json:"total_cash_amount"`
	TotalCardAmount  float64            `json:"total_card_amount"`
	TotalOrderAmount float64            `json:"total_order_amount"`
	ShiftDetails     []UserShiftDetails `json:"shift_details"`
}

type UserShiftDetails struct {
	ShiftID       uint64     `json:"shift_id"`
	ActiveShift   bool       `json:"active_shift"`
	LoginTime     time.Time  `json:"login_time"`
	LogoutTime    *time.Time `json:"logout_time"`
	ShiftDuration string     `json:"shift_duration"`
	OrderCount    int64      `json:"order_count"`
	CashAmount    float64    `json:"cash_amount"`
	CardAmount    float64    `json:"card_amount"`
	TotalAmount   float64    `json:"total_amount"`
}

type ShopShiftInfoResponse struct {
	ShopID           uint32             `json:"shop_id"`
	FromDate         time.Time          `json:"from_date"`
	ToDate           time.Time          `json:"to_date"`
	OrderCount       int64              `json:"order_count"`
	TotalCashAmount  float64            `json:"total_cash_amount"`
	TotalCardAmount  float64            `json:"total_card_amount"`
	TotalOrderAmount float64            `json:"total_order_amount"`
	ShiftDetails     []ShopShiftDetails `json:"shift_details"`
}

type ShopShiftDetails struct {
	UserID      uint32  `json:"user_id"`
	OrderCount  int64   `json:"order_count"`
	CashAmount  float64 `json:"cash_amount"`
	CardAmount  float64 `json:"card_amount"`
	TotalAmount float64 `json:"total_amount"`
}

type GetCashDrawerSessionsResp struct {
	ID                            uint64    `json:"id"`
	CashDrawerID                  uint64    `json:"cash_drawer_id"`
	SessionStartedUserID          uint64    `json:"session_started_user_id"`
	SessionStartedUserName        string    `json:"session_started_user_name"`
	SessionEndedUserID            int64     `json:"session_ended_user_id"`
	SessionEndedUserName          string    `json:"session_ended_user_name"`
	OpenedAt                      time.Time `json:"opened_at"`
	OpeningBalance                float64   `json:"opening_balance"`
	ClosedAt                      time.Time `json:"closed_at"`
	ClosingBalanceCounted         float64   `json:"closing_balance_counted"`
	ClosingBalanceExpected        float64   `json:"closing_balance_expected"`
	Difference                    float64   `json:"difference"`
	TotalInAmount                 float64   `json:"total_in_amount"`
	TotalOutAmount                float64   `json:"total_out_amount"`
	TotalSalesAmount              float64   `json:"total_sales_amount"`
	TotalOtherSalesAmount         float64   `json:"total_other_sales_amount"`
	TotalRefundAmount             float64   `json:"total_refund_amount"`
	TotalCashSaleCashRefundAmount float64   `json:"total_cash_sale_cash_refund_amount"`
	TotalCardSaleCashRefundAmount float64   `json:"total_card_sale_cash_refund_amount"`
	TotalOtherRefundAmount        float64   `json:"total_other_cash_sale_cash_refund_amount"`
	Status                        string    `json:"status"`
	CreatedAt                     time.Time `json:"created_at"`
	UpdatedAt                     time.Time `json:"updated_at"`
}

type GetActiveCashDrawerSessionInfoResp struct {
	ID                            uint64    `json:"id"`
	CashDrawerID                  uint64    `json:"cash_drawer_id"`
	SessionStartedUserID          uint64    `json:"session_started_user_id"`
	SessionStartedUserName        string    `json:"session_started_user_name"`
	OpenedAt                      time.Time `json:"opened_at"`
	OpeningBalance                float64   `json:"opening_balance"`
	ClosingBalanceExpected        float64   `json:"closing_balance_expected"`
	TotalInAmount                 float64   `json:"total_in_amount"`
	TotalOutAmount                float64   `json:"total_out_amount"`
	TotalSalesAmount              float64   `json:"total_sales_amount"`
	TotalOtherSalesAmount         float64   `json:"total_other_sales_amount"`
	TotalRefundAmount             float64   `json:"total_refund_amount"`
	TotalCashSaleCashRefundAmount float64   `json:"total_cash_sale_cash_refund_amount"`
	TotalCardSaleCashRefundAmount float64   `json:"total_card_sale_cash_refund_amount"`
	TotalOtherRefundAmount        float64   `json:"total_other_cash_sale_cash_refund_amount"`
	Status                        string    `json:"status"`
	CreatedAt                     time.Time `json:"created_at"`
	UpdatedAt                     time.Time `json:"updated_at"`

	IncompleteOrders ActiveSessionIncompleteOrdersResponse `json:"incomplete_orders"`
}

type ActiveSessionIncompleteOrdersResponse struct {
	Count  int      `json:"count"`
	Orders []string `json:"orders"`
}

type GetCashDrawerTransactionHistoryResp struct {
	CashDrawerID        uint64    `json:"cash_drawer_id"`
	CashDrawerSessionID uint64    `json:"cash_drawer_session_id"`
	CashMovementID      uint64    `json:"cash_movement_id"`
	CreatedAt           time.Time `json:"created_at"`
	MovementType        string    `json:"movement_type"`
	Note                string    `json:"note"`
	Amount              float64   `json:"amount"`
	UserName            string    `json:"user_name"`
}

type GetShopConfigResponse struct {
	BrandID    int32           `json:"brand_id"`
	ShopID     int32           `json:"shop_id"`
	TerminalID int32           `json:"terminal_id"`
	ConfigType string          `json:"config_type"`
	Config     json.RawMessage `json:"config"`
}

type DgPosTmpPaymentResponse struct {
	TypeID        string  `json:"type_id"`
	Type          string  `json:"type"`
	PaymentMode   string  `json:"payment_mode"`
	PaymentAmount float64 `json:"payment_amount"`
	TransactionID string  `json:"transaction_id"`
}
