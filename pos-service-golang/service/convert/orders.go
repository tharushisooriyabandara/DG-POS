package convert

import (
	"cmp"
	"database/sql"
	"fmt"
	"math"
	"strconv"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service/generate"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

type Order struct {
	*db.Order
	OrderItems       []*OrderItem
	OrderCommission  *db.OrderCommission
	OrderPayments    []*db.DgPosPayment
	OrderLocation    *db.DgPosOrderLocation
	OrderTimestamps  *db.DgPosOrderStatusTimestamp
	VoucherCodes     []string
	OrderShopFees    []*db.OrderShopFee
	OrderTaxes       []*db.OrderTax
	OrderTransaction []*db.OrderTransaction
}

type OrderItem struct {
	*db.OrderItem
	ModifierDetails []*ModifierDetails
	OrderItemTax    *db.OrderItemTax
}

type ModifierDetails struct {
	*db.OrderItemModifier
	OrderItemModifierTax *db.OrderItemTax
}

func OrderRequestToOrder(orderRequest *types.CreateOrderRequest) *Order {
	paymentModeString := generate.PaymentModeString(orderRequest.OrderPayments)
	vouchers := generate.SerializedVoucherString(orderRequest.Vouchers)

	voucherCodes := make([]string, 0, len(orderRequest.Vouchers))
	for _, voucher := range orderRequest.Vouchers {
		voucherCodes = append(voucherCodes, voucher.VoucherCode)
	}

	return &Order{
		Order: &db.Order{
			ID:                        0,
			CreatedAt:                 sql.NullTime{Time: time.Now().UTC(), Valid: true},
			CustomerID:                sql.NullInt32{Int32: orderRequest.CustomerID, Valid: true},
			CustomerName:              sql.NullString{String: orderRequest.CustomerName, Valid: true},
			DeliveryDateTime:          parseDeliveryDateTime(orderRequest.DeliveryDateTime),
			PlatformID:                orderRequest.DeliveryPlatformID,
			Discount:                  calculateAmount(orderRequest.Discount + voucherTotal(orderRequest.Vouchers)),
			VoucherDiscount:           calculateAmount(voucherTotal(orderRequest.Vouchers)),
			DiscountModeApplied:       sql.NullString{String: orderRequest.DiscountModeApplied, Valid: orderRequest.DiscountModeApplied != "" && orderRequest.DiscountModeApplied != " "},
			DiscountPercentageApplied: sql.NullInt32{Int32: calculateAmount(orderRequest.DiscountPercentage), Valid: orderRequest.DiscountPercentage != 0},
			DiscountType:              sql.NullString{String: orderRequest.DiscountType, Valid: orderRequest.DiscountType != ""},
			DisplayOrderID:            sql.NullString{String: orderRequest.DisplayOrderID, Valid: orderRequest.DisplayOrderID != ""},
			Note:                      sql.NullString{String: orderRequest.OrderNote, Valid: orderRequest.OrderNote != ""},
			PaymentMode:               sql.NullString{String: paymentModeString, Valid: paymentModeString != ""},
			RemoteOrderID:             generate.RemoteOrderID(len(orderRequest.OrderItems)),
			ShippingMethod:            sql.NullString{String: orderRequest.ShippingMethod, Valid: true},
			ShippingTax:               sql.NullString{String: fmt.Sprintf("%.2f", orderRequest.ShippingTax), Valid: orderRequest.ShippingTax != 0},
			ShippingTotal:             sql.NullString{String: fmt.Sprintf("%.2f", orderRequest.ShippingTotal), Valid: orderRequest.ShippingTotal != 0},
			DeliveryTax:               sql.NullInt32{Int32: calculateAmount(orderRequest.DeliveryTax.TaxAmount), Valid: orderRequest.DeliveryTax.TaxAmount != 0},
			TaxID:                     sql.NullInt32{Int32: orderRequest.DeliveryTax.TaxID, Valid: orderRequest.DeliveryTax.TaxID != 0},
			ShopID:                    orderRequest.ShopID,
			SubTotal:                  sql.NullInt32{Int32: calculateAmount(orderRequest.SubTotal), Valid: true},
			DgposTableID:              sql.NullInt32{Int32: int32(orderRequest.TableID), Valid: orderRequest.TableID != 0},
			Tip:                       fmt.Sprintf("%.2f", orderRequest.Tip),
			TipPercentage:             fmt.Sprintf("%.2f", orderRequest.TipPercentage),
			TotalAmount:               calculateAmount(orderRequest.TotalAmount),
			TotalTax:                  sql.NullString{String: fmt.Sprintf("%.2f", orderRequest.TotalTax), Valid: true},
			UpdatedAt:                 sql.NullTime{Time: time.Now().UTC(), Valid: true},
			UserID:                    sql.NullInt32{Int32: orderRequest.UserID, Valid: true},
			Vouchers:                  sql.NullString{String: vouchers, Valid: vouchers != ""},
			TotalFee:                  sql.NullInt32{Int32: calculateAmount(orderRequest.TotalFee), Valid: true},
			CancelledByCustomer:       false,
			CashDue:                   "0.00",
			DevicePlatform:            sql.NullString{String: "dg_pos", Valid: true},
			IsScheduled:               false,
			IsTableOrder:              false,
			OrderDelayed:              false,
			OrderTypeID:               sql.NullInt32{Int32: int32(4), Valid: true},
			Status:                    "QUEUE",
			Surcharge:                 "0.00",
			TestingOrder:              false,
			PaymentMethod:             sql.NullString{},
			CampaignCode:              sql.NullString{},
			CancelledReason:           sql.NullString{},
			ContactAccessCode:         sql.NullString{},
			DeliveryLocationID:        sql.NullInt32{},
			TableOrderMethodID:        sql.NullInt32{},
			UniqueOrderID:             sql.NullString{},
		},
		OrderTimestamps: &db.DgPosOrderStatusTimestamp{
			Queue:     sql.NullTime{},
			Preparing: sql.NullTime{},
			Ready:     sql.NullTime{},
			Served:    sql.NullTime{},
			Delivered: sql.NullTime{},
			Completed: sql.NullTime{},
			CreatedAt: sql.NullTime{Time: time.Now().UTC(), Valid: true},
			UpdatedAt: sql.NullTime{Time: time.Now().UTC(), Valid: true},
		},
		OrderItems:       orderItems(orderRequest.OrderItems, "QUEUE"),
		OrderPayments:    orderPayments(orderRequest.OrderPayments),
		OrderCommission:  OrderCommission(0),
		OrderLocation:    nil,
		VoucherCodes:     voucherCodes,
		OrderShopFees:    orderShopFees(orderRequest.OrderShopFees),
		OrderTaxes:       orderTaxes(orderRequest.OrderTaxes),
		OrderTransaction: OrderTransactions(orderRequest.OrderPayments),
	}
}

func OrderTransactions(payments []*types.OrderPayment) []*db.OrderTransaction {
	orderTransactions := make([]*db.OrderTransaction, 0, len(payments))
	for _, payment := range payments {
		paymentType := "CASH"
		if payment.PaymentMethod == "CARD" {
			paymentType = "CARD_MACHINE"
		}
		orderTransactions = append(orderTransactions, &db.OrderTransaction{
			TransactionType:   "SALE",
			TransactionMode:   sql.NullString{String: payment.PaymentMethod, Valid: true},
			TransactionAmount: calculateAmount(payment.PayingAmount),
			PaymentType:       sql.NullString{String: paymentType, Valid: true},
		})
	}
	return orderTransactions
}

func orderTaxes(orderTaxes []*types.OrderTax) []*db.OrderTax {
	orderTaxesDB := make([]*db.OrderTax, 0, len(orderTaxes))
	for _, orderTax := range orderTaxes {
		orderTaxesDB = append(orderTaxesDB, &db.OrderTax{
			TaxRate:       fmt.Sprintf("%.2f", orderTax.Tax_rate),
			TaxCode:       orderTax.Tax_code,
			TaxAmount:     calculateAmount(orderTax.Tax_amount),
			TaxableAmount: calculateAmount(orderTax.Taxable_amount),
			Type:          "SALE",
		})
	}
	return orderTaxesDB
}

func orderShopFees(orderShopFees []*types.OrderShopFee) []*db.OrderShopFee {
	orderShopFeesDB := make([]*db.OrderShopFee, 0, len(orderShopFees))
	for _, orderShopFee := range orderShopFees {
		orderShopFeesDB = append(orderShopFeesDB, &db.OrderShopFee{
			ID:         0,
			OrderID:    0,
			ShopFeeID:  int32(orderShopFee.ShopFeeId),
			Amount:     int32(calculateAmount(orderShopFee.Amount)),
			ShopFeeTax: sql.NullInt32{Int32: int32(calculateAmount(orderShopFee.TaxAmount)), Valid: orderShopFee.TaxAmount != 0},
			TaxID:      sql.NullInt32{Int32: orderShopFee.TaxID, Valid: orderShopFee.TaxID != 0},
			CreatedAt:  sql.NullTime{Time: time.Now().UTC(), Valid: true},
			UpdatedAt:  sql.NullTime{Time: time.Now().UTC(), Valid: true},
		})
	}
	return orderShopFeesDB
}

func orderItems(orderItems []*types.OrderItem, itemStatus string) []*OrderItem {
	items := make([]*OrderItem, 0, len(orderItems))
	for _, item := range orderItems {
		items = append(items, orderItemRequestToOrderItem(item, itemStatus))
	}
	return items
}

func orderPayments(orderPayments []*types.OrderPayment) []*db.DgPosPayment {
	dgPosPayments := make([]*db.DgPosPayment, 0, len(orderPayments))
	for _, payment := range orderPayments {
		dgPosPayments = append(dgPosPayments, &db.DgPosPayment{
			ID:            0,
			OrderID:       0,
			TransactionID: sql.NullInt64{},
			DateTime:      sql.NullTime{Time: time.Now().UTC(), Valid: true},
			Amount:        strconv.Itoa(int(calculateAmount(payment.PayingAmount))),
			Cash:          strconv.Itoa(int(calculateAmount(payment.Cash))),
			Balance:       strconv.Itoa(int(calculateAmount(payment.Balance))),
			CardTransactionToken: sql.NullString{
				String: payment.TransactionID,
				Valid:  payment.PaymentMethod == "CARD" && payment.TransactionID != "",
			},
			RefundID:        sql.NullString{},
			PaymentMethodID: sql.NullString{String: parsePaymentMethodID(payment.PaymentMethod), Valid: true},
			Status:          sql.NullString{String: "PAID", Valid: true},
			IsRefund:        false,
			PaymentType:     sql.NullString{String: payment.PaymentMethod, Valid: true},
			CreatedAt:       sql.NullTime{Time: time.Now().UTC(), Valid: true},
			UpdatedAt:       sql.NullTime{Time: time.Now().UTC(), Valid: true},
		})
	}
	return dgPosPayments
}

func OrderCommission(orderID int64) *db.OrderCommission {
	return &db.OrderCommission{
		ID:         0,
		OrderID:    orderID,
		Commission: "",
		Status:     "PENDING",
		OrderTmpID: sql.NullInt64{},
		Reference:  sql.NullString{},
		ReportID:   sql.NullInt32{},
		CreatedAt:  sql.NullTime{Time: time.Now().UTC(), Valid: true},
		UpdatedAt:  sql.NullTime{Time: time.Now().UTC(), Valid: true},
	}
}

func OrderLocation(or *db.GetOrderReceiverRow) *db.DgPosOrderLocation {
	if or == nil {
		return nil
	}
	return &db.DgPosOrderLocation{
		ID:           0,
		OrderID:      0,
		FirstName:    or.Customer.FirstName.String,
		LastName:     or.Customer.LastName.String,
		Email:        or.Customer.Email,
		CountryCode:  or.Customer.CountryCode.String,
		Country:      or.Customer.Country,
		Phone:        or.Customer.Phone.String,
		FlatNo:       or.DeliveryLocation.FlatNo,
		HouseNo:      or.DeliveryLocation.HouseNo,
		AddressLine1: or.DeliveryLocation.AddressLine1,
		AddressLine2: or.DeliveryLocation.AddressLine2,
		Landmark:     or.DeliveryLocation.Landmark,
		City:         or.DeliveryLocation.City,
		Postcode:     or.DeliveryLocation.PostalCode,
		Latitude:     or.DeliveryLocation.Latitude,
		Longitude:    or.DeliveryLocation.Longitude,
		CreatedAt:    sql.NullTime{Time: time.Now().UTC(), Valid: true},
		UpdatedAt:    sql.NullTime{Time: time.Now().UTC(), Valid: true},
	}
}

func orderItemRequestToOrderItem(item *types.OrderItem, itemStatus string) *OrderItem {
	var orderItemTax *db.OrderItemTax
	if item.TaxDetails != nil {
		orderItemTax = &db.OrderItemTax{
			ID:                  0,
			OrderID:             0,
			OrderItemID:         0,
			OrderItemModifierID: sql.NullInt32{},
			ItemPrice:           calculateAmount(item.PricePerItem) * int32(item.Quantity),
			TaxID:               item.TaxDetails.TaxID,
			TaxProfileID:        item.TaxDetails.TaxProfileID,
			TaxRuleID:           item.TaxDetails.TaxRuleID,
			Amount:              calculateAmount(item.TaxDetails.Amount),
		}
	}

	return &OrderItem{
		OrderItem: &db.OrderItem{
			OrderID:        0,
			CategoryName:   sql.NullString{},
			DiscountAmount: calculateAmount(item.DiscountAmount),
			IsSale:         item.IsSale,
			ItemID:         strconv.Itoa(item.ItemID),
			ItemName:       item.ItemName,
			Modifiers:      sql.NullString{},
			Note:           sql.NullString{String: item.Note, Valid: item.Note != ""},
			OriginalPrice:  cmp.Or(calculateAmount(item.OriginalPrice), calculateAmount(item.PricePerItem)),
			PricePerItem:   calculateAmount(item.PricePerItem),
			Quantity:       float64(item.Quantity),
			Status:         itemStatus,
			Tax:            calculateAmount(item.Tax),
			Total:          calculateAmount(item.Total),
			CreatedAt:      sql.NullTime{Time: time.Now().UTC(), Valid: true},
			UpdatedAt:      sql.NullTime{Time: time.Now().UTC(), Valid: true},
		},
		OrderItemTax:    orderItemTax,
		ModifierDetails: orderItemModifiers(item.ModifierDetails, int32(item.Quantity)),
	}
}

func orderItemModifiers(itemModifiers []*types.ModifierDetails, mainQty int32) []*ModifierDetails {

	modifiers := []*ModifierDetails{}

	for _, modifier := range itemModifiers {
		modifiers = append(modifiers, orderItemModifierRequestToOrderItemModifier("", modifier, mainQty))
		if len(modifier.Modifiers) > 0 {
			for _, m := range modifier.Modifiers {
				modifiers = append(
					modifiers,
					orderItemModifierRequestToOrderItemModifier(modifier.ModifierItem.ExternalItemID, &types.ModifierDetails{
						ModifierGroupName: m.ModifierMain.Title,
						ModifierMainItem:  m.ModifierMain.Id,
						Quantity:          m.ModifierMain.Quantity,
						ModifierItem:      m.ModifierItem,
					}, mainQty),
				)
			}
		}
	}

	return modifiers
}

func orderItemModifierRequestToOrderItemModifier(parentModifierID string, modifier *types.ModifierDetails, mainQty int32) *ModifierDetails {
	var orderItemModifierTax *db.OrderItemTax
	if modifier.ModifierItem.TaxDetails != nil {
		orderItemModifierTax = &db.OrderItemTax{
			ID:                  0,
			OrderID:             0,
			OrderItemID:         0,
			OrderItemModifierID: sql.NullInt32{},
			ItemPrice:           calculateAmount(modifier.ModifierItem.Price) * mainQty,
			TaxID:               modifier.ModifierItem.TaxDetails.TaxID,
			TaxProfileID:        modifier.ModifierItem.TaxDetails.TaxProfileID,
			TaxRuleID:           modifier.ModifierItem.TaxDetails.TaxRuleID,
			Amount:              calculateAmount(modifier.ModifierItem.TaxDetails.Amount),
		}
	}

	return &ModifierDetails{
		OrderItemModifier: &db.OrderItemModifier{
			ID:                  0,
			OrderItemID:         0,
			Amount:              strconv.Itoa(int(calculateAmount(modifier.ModifierItem.Price))),
			ModifierGroupName:   modifier.ModifierGroupName,
			ModifierID:          strconv.Itoa(modifier.ModifierMainItem),
			ModifierOptionID:    modifier.ModifierItem.ExternalItemID,
			ModifierOptionName:  modifier.ModifierItem.ItemName,
			ParentModifierID:    sql.NullString{String: parentModifierID, Valid: parentModifierID != ""},
			Quantity:            strconv.Itoa(modifier.Quantity),
			SubParentModifierID: sql.NullString{},
			CreatedAt:           sql.NullTime{Time: time.Now().UTC(), Valid: true},
			UpdatedAt:           sql.NullTime{Time: time.Now().UTC(), Valid: true},
		},
		OrderItemModifierTax: orderItemModifierTax,
	}
}

func OrderPaymentsRequestToOrderPayments(orderPaymentRequest *types.CreateOrderPaymentRequest) []*db.DgPosPayment {
	dgPosPayments := orderPayments(orderPaymentRequest.Payments)
	for _, payment := range dgPosPayments {
		payment.OrderID = int32(orderPaymentRequest.OrderID)
		payment.DateTime = sql.NullTime{Time: time.Now().UTC(), Valid: true}
		payment.CreatedAt = sql.NullTime{Time: time.Now().UTC(), Valid: true}
		payment.UpdatedAt = sql.NullTime{Time: time.Now().UTC(), Valid: true}
	}
	return dgPosPayments
}

func OrderUpdateRequestToOrder(existingOrder *db.Order, orderRequest *types.UpdateOrderRequest) *Order {
	vouchers := generate.SerializedVoucherString(orderRequest.Vouchers)
	voucherCodes := make([]string, 0, len(orderRequest.Vouchers))
	for _, voucher := range orderRequest.Vouchers {
		voucherCodes = append(voucherCodes, voucher.VoucherCode)
	}

	return &Order{
		OrderItems:      orderItems(orderRequest.OrderItems, existingOrder.Status),
		OrderCommission: OrderCommission(int64(existingOrder.ID)),
		VoucherCodes:    voucherCodes,
		OrderShopFees:   orderShopFees(orderRequest.OrderShopFees),
		OrderTaxes:      orderTaxes(orderRequest.OrderTaxes),
		OrderTimestamps: &db.DgPosOrderStatusTimestamp{
			Queue:     existingOrder.CreatedAt,
			Preparing: existingOrder.CreatedAt,
			Ready:     existingOrder.CreatedAt,
			Served:    existingOrder.CreatedAt,
			Delivered: existingOrder.CreatedAt,
			Completed: existingOrder.CreatedAt,
			CreatedAt: existingOrder.CreatedAt,
			UpdatedAt: sql.NullTime{Time: time.Now().UTC(), Valid: true},
		},
		Order: &db.Order{
			ID:                        existingOrder.ID,
			CustomerID:                sql.NullInt32{Int32: orderRequest.CustomerID, Valid: true},
			CustomerName:              sql.NullString{String: orderRequest.CustomerName, Valid: true},
			Discount:                  calculateAmount(orderRequest.Discount + voucherTotal(orderRequest.Vouchers)),
			VoucherDiscount:           calculateAmount(voucherTotal(orderRequest.Vouchers)),
			DiscountModeApplied:       sql.NullString{String: orderRequest.DiscountModeApplied, Valid: orderRequest.DiscountModeApplied != "" && orderRequest.DiscountModeApplied != " "},
			DiscountPercentageApplied: sql.NullInt32{Int32: calculateAmount(orderRequest.DiscountPercentage), Valid: orderRequest.DiscountPercentage != 0},
			DiscountType:              sql.NullString{String: orderRequest.DiscountType, Valid: orderRequest.DiscountType != ""},
			Note:                      sql.NullString{String: orderRequest.OrderNote, Valid: orderRequest.OrderNote != ""},
			ShippingMethod:            sql.NullString{String: orderRequest.ShippingMethod, Valid: true},
			ShippingTax:               sql.NullString{String: fmt.Sprintf("%.2f", orderRequest.ShippingTax), Valid: orderRequest.ShippingTax != 0},
			ShippingTotal:             sql.NullString{String: fmt.Sprintf("%.2f", orderRequest.ShippingTotal), Valid: orderRequest.ShippingTotal != 0},
			SubTotal:                  sql.NullInt32{Int32: calculateAmount(orderRequest.SubTotal), Valid: true},
			DgposTableID:              sql.NullInt32{Int32: int32(orderRequest.TableID), Valid: orderRequest.TableID != 0},
			Tip:                       fmt.Sprintf("%.2f", orderRequest.Tip),
			TipPercentage:             fmt.Sprintf("%.2f", orderRequest.TipPercentage),
			TotalAmount:               calculateAmount(orderRequest.TotalAmount),
			TotalTax:                  sql.NullString{String: fmt.Sprintf("%.2f", orderRequest.TotalTax), Valid: true},
			UpdatedAt:                 sql.NullTime{Time: time.Now().UTC(), Valid: true},
			UserID:                    sql.NullInt32{Int32: orderRequest.UserID, Valid: true},
			Vouchers:                  sql.NullString{String: vouchers, Valid: vouchers != ""},
			TotalFee:                  sql.NullInt32{Int32: calculateAmount(orderRequest.TotalFee), Valid: true},
			Status:                    existingOrder.Status,
			CancelledByCustomer:       existingOrder.CancelledByCustomer,
			CancelledReason:           existingOrder.CancelledReason,
			CampaignCode:              existingOrder.CampaignCode,
			CashDue:                   existingOrder.CashDue,
			ContactAccessCode:         existingOrder.ContactAccessCode,
			CreatedAt:                 existingOrder.CreatedAt,
			DeliveryDateTime:          existingOrder.DeliveryDateTime,
			DeliveryLocationID:        existingOrder.DeliveryLocationID,
			PlatformID:                existingOrder.PlatformID,
			DevicePlatform:            existingOrder.DevicePlatform,
			DisplayOrderID:            existingOrder.DisplayOrderID,
			IsScheduled:               existingOrder.IsScheduled,
			IsTableOrder:              existingOrder.IsTableOrder,
			OrderDelayed:              existingOrder.OrderDelayed,
			OrderTypeID:               existingOrder.OrderTypeID,
			PaymentMethod:             existingOrder.PaymentMethod,
			RemoteOrderID:             existingOrder.RemoteOrderID,
			ShopID:                    existingOrder.ShopID,
			Surcharge:                 existingOrder.Surcharge,
			TableOrderMethodID:        existingOrder.TableOrderMethodID,
			TestingOrder:              existingOrder.TestingOrder,
			UniqueOrderID:             existingOrder.UniqueOrderID,
		},
	}
}

func parseDeliveryDateTime(timestamp string) time.Time {
	// date format is already validated therefore it is safe to ignore the error
	deliveryDateTime, _ := time.Parse(time.DateTime, timestamp)
	return deliveryDateTime
}

func calculateAmount(amount float64) int32 {
	return int32(math.Round(amount * 100))
}

func parsePaymentMethodID(paymentMethod string) string {
	if paymentMethod == "CARD" {
		return "2"
	}
	return "4"
}

func voucherTotal(vouchers []*types.OrderVoucher) float64 {
	var total float64
	for _, voucher := range vouchers {
		total += voucher.VoucherDiscount
	}
	return total
}
