package wrap

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"strings"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	awssqs "github.com/Delivergate-Dev/pos-service-golang/mq/aws-sqs"
	"github.com/Delivergate-Dev/pos-service-golang/service/generate"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/elliotchance/phpserialize"
	"go.uber.org/zap"
)

type customerSQSWrapper struct {
	customerService
	db     *sql.DB
	logger *zap.Logger
	byPass bool
}

func NewCustomerSQSWrapper(bypass bool, db *sql.DB, logger *zap.Logger, service customerService) customerService {
	return &customerSQSWrapper{
		customerService: service,
		db:              db,
		logger:          logger,
		byPass:          bypass,
	}
}

func (c *customerSQSWrapper) CreateCustomer(ctx context.Context, createCustomerRequest *types.CreateCustomerRequest) (*types.GetCustomersResponse, error) {
	customer, err := c.customerService.CreateCustomer(ctx, createCustomerRequest)
	if err != nil {
		return nil, err
	}

	if c.ServiceBypassed() {
		return customer, nil
	}

	msgBody, err := c.createMessageBody(ctx, customer.ID, createCustomerRequest.Requestor.TenantCode)
	if err != nil {
		c.logger.Error("Failed to create sqs message body after customer creation", zap.Error(err))
		return customer, nil
	}

	awssqs.Send(ctx, awssqs.Message{
		QueueName:       "customers",
		MessageBody:     string(msgBody),
		MessageGroupId:  fmt.Sprintf("%d", customer.ID),
		DeduplicationID: fmt.Sprintf("%d%d", customer.ID, time.Now().UTC().Unix()),
	})

	return customer, nil
}

func (c *customerSQSWrapper) UpdateCustomer(ctx context.Context, updateCustomerRequest *types.UpdateCustomerRequest) error {
	err := c.customerService.UpdateCustomer(ctx, updateCustomerRequest)
	if err != nil {
		return err
	}

	if c.ServiceBypassed() {
		return nil
	}

	msgBody, err := c.createMessageBody(ctx, updateCustomerRequest.ID, updateCustomerRequest.Requestor.TenantCode)
	if err != nil {
		c.logger.Error("Failed to create sqs message body after customer update", zap.Error(err))
		return nil
	}

	awssqs.Send(ctx, awssqs.Message{
		QueueName:       "customers",
		MessageBody:     string(msgBody),
		MessageGroupId:  fmt.Sprintf("%d", updateCustomerRequest.ID),
		DeduplicationID: fmt.Sprintf("%d%d", updateCustomerRequest.ID, time.Now().UTC().Unix()),
	})

	return nil
}

func (c *customerSQSWrapper) createMessageBody(ctx context.Context, customerId uint64, masterTenantCode string) (json.RawMessage, error) {
	queries := db.New(c.db)
	dbCustomer, err := queries.GetCustomerByID(ctx, customerId)
	if err != nil {
		return nil, fmt.Errorf("failed to get customer: %w", err)
	}

	brand, err := queries.GetWebshopBrandById(ctx, uint32(dbCustomer.AccountBrand.Int32))
	if err != nil {
		return nil, fmt.Errorf("failed to get brand: %w", err)
	}

	mobileAppOrders, err := queries.GetMobileAppOrdersCount(ctx, sql.NullInt32{Int32: int32(dbCustomer.ID), Valid: true})
	if err != nil {
		return nil, fmt.Errorf("failed to get mobile app orders: %w", err)
	}

	mobileAppReservations, err := queries.GetMobileAppReservationsCount(ctx, sql.NullInt32{Int32: int32(dbCustomer.ID), Valid: true})
	if err != nil {
		return nil, fmt.Errorf("failed to get mobile app reservations: %w", err)
	}

	content := serializedContent{
		"customer": serializedContent{
			"customer": newCustomer(
				*dbCustomer,
				masterTenantCode,
				*brand,
				mobileAppOrders,
				mobileAppReservations,
			),
		},
	}

	phpSerialized, err := phpserialize.Marshal(content, phpserialize.DefaultMarshalOptions())
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

func newCustomer(c db.Customer, tenantCode string, brand db.WebshopBrand, mobileAppOrders, mobileAppReservations int64) serializedContent {
	return serializedContent{
		"id":                               c.ID,
		"tenant_code":                      tenantCode,
		"brand_id":                         c.AccountBrand.Int32,
		"brand_name":                       brand.BrandName,
		"brand_code":                       brand.BrandCode,
		"first_name":                       nullableStr(c.FirstName.String),
		"key_id_first_name":                c.KeyIDFirstName,
		"hashed_first_name":                nullableStr(c.HashedFirstName.String),
		"last_name":                        nullableStr(c.LastName.String),
		"key_id_last_name":                 c.KeyIDLastName,
		"hashed_last_name":                 nullableStr(c.HashedLastName.String),
		"dob":                              nullableStr(c.Dob.String),
		"key_id_dob":                       c.KeyIDDob,
		"hashed_dob":                       nullableStr(c.HashedDob.String),
		"address":                          nullableStr(c.Address1.String),
		"key_id_address":                   c.KeyIDAddress1,
		"hashed_address":                   nullableStr(c.HashedAddress1.String),
		"postcode":                         nullableStr(c.Postcode.String),
		"hashed_postcode":                  nullableStr(c.HashedPostcode.String),
		"key_id_postcode":                  c.KeyIDPostcode,
		"country_code":                     nullableStr(c.CountryCode.String),
		"phone":                            nullableStr(c.Phone.String),
		"hashed_phone":                     nullableStr(c.HashedPhone.String),
		"key_id_phone":                     c.KeyIDPhone,
		"old_email":                        nullableStr(c.Email.String),
		"email":                            nullableStr(c.Email.String),
		"key_id_email":                     c.KeyIDEmail,
		"hashed_email":                     nullableStr(c.HashedEmail.String),
		"first_order_offer_eligible":       boolToInt(c.FirstOrderOfferEligible),
		"expo_token":                       nullableStr(c.ExpoToken.String),
		"notification_permission_given_at": nullableTime(c.NotificationPermissionGivenAt.Time),
		"has_mobile_app":                   hasMobileApp(mobileAppOrders, mobileAppReservations),
		"status":                           boolToInt(c.Status),
		"subscribe_to_promotion_emails":    nullableBool(c.SubscribeToPromotionEmails.Bool),
		"promotion_emails_subscribed_at":   nullableTime(c.PromotionEmailsSubscribedAt.Time),
		"device_platform":                  nullableStr(c.DevicePlatform.String),
		"last_active_time":                 nullableTime(c.LastActiveTime.Time),
		"last_active_device":               nullableStr(c.LastActiveDevice.String),
		"created_at":                       nullableTime(c.CreatedAt.Time),
		"updated_at":                       nullableTime(c.UpdatedAt.Time),
		"token":                            token(brand.BrandCode),
	}
}

func boolToInt(b bool) int {
	if b {
		return 1
	}
	return 0
}

func hasMobileApp(mobileAppOrders, mobileAppReservations int64) int32 {
	if mobileAppOrders > 0 {
		return 1
	}
	if mobileAppReservations > 0 {
		return 1
	}
	return 0
}

func token(brandCode string) string {
	randomStr := generate.RandomString(18)
	date := time.Now().Format("020106") // DDMMYY
	return fmt.Sprintf("%s-%s-%s-%s-%s", randomStr[:8], strings.ToLower(brandCode), randomStr[8:12], date, randomStr[12:])
}

func (c *customerSQSWrapper) ServiceBypassed() bool {
	return c.byPass
}
