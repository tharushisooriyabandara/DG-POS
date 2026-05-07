package convert

import (
	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
)

func OrderToOrderParams(order *Order) *db.CreateOrderParams {
	return &db.CreateOrderParams{
		CustomerID:                order.CustomerID,
		CustomerName:              order.CustomerName,
		DeliveryDateTime:          order.DeliveryDateTime,
		DeliveryPlatformID:        order.PlatformID,
		Discount:                  order.Discount,
		VoucherDiscount:           order.VoucherDiscount,
		DiscountModeApplied:       order.DiscountModeApplied,
		DiscountPercentageApplied: order.DiscountPercentageApplied,
		DiscountType:              order.DiscountType,
		DisplayOrderID:            order.DisplayOrderID,
		Note:                      order.Note,
		PaymentMethod:             order.PaymentMethod,
		PaymentMode:               order.PaymentMode,
		RemoteOrderID:             order.RemoteOrderID,
		ShippingMethod:            order.ShippingMethod,
		ShippingTax:               order.ShippingTax,
		ShippingTotal:             order.ShippingTotal,
		DeliveryTax:               order.DeliveryTax,
		TaxID:                     order.TaxID,
		ShopID:                    order.ShopID,
		SubTotal:                  order.SubTotal,
		DgposTableID:              order.DgposTableID,
		Tip:                       order.Tip,
		TipPercentage:             order.TipPercentage,
		TotalAmount:               order.TotalAmount,
		TotalTax:                  order.TotalTax,
		UserID:                    order.UserID,
		CancelledByCustomer:       order.CancelledByCustomer,
		CashDue:                   order.CashDue,
		DevicePlatform:            order.DevicePlatform,
		IsScheduled:               order.IsScheduled,
		IsTableOrder:              order.IsTableOrder,
		OrderDelayed:              order.OrderDelayed,
		OrderTypeID:               order.OrderTypeID,
		Status:                    order.Status,
		Surcharge:                 order.Surcharge,
		TestingOrder:              order.TestingOrder,
		Vouchers:                  order.Vouchers,
		CampaignCode:              order.CampaignCode,
		CancelledReason:           order.CancelledReason,
		ContactAccessCode:         order.ContactAccessCode,
		DeliveryLocationID:        order.DeliveryLocationID,
		TableOrderMethodID:        order.TableOrderMethodID,
		TotalFee:                  order.TotalFee,
		UniqueOrderID:             order.UniqueOrderID,
	}
}

func OrderItemToOrderItemParams(orderID uint64, item *OrderItem) *db.CreateOrderItemParams {
	return &db.CreateOrderItemParams{
		CategoryName:   item.CategoryName,
		DiscountAmount: item.DiscountAmount,
		IsSale:         item.IsSale,
		ItemID:         item.ItemID,
		ItemName:       item.ItemName,
		Modifiers:      item.Modifiers,
		Note:           item.Note,
		OrderID:        int32(orderID),
		OriginalPrice:  item.OriginalPrice,
		PricePerItem:   item.PricePerItem,
		Quantity:       item.Quantity,
		Status:         item.Status,
		Tax:            item.Tax,
		Total:          item.Total,
		CreatedAt:      item.CreatedAt,
		UpdatedAt:      item.UpdatedAt,
	}
}

func OrderItemModifiersToOrderItemModifierParams(modifier *ModifierDetails) *db.CreateOrderItemModifierParams {
	return &db.CreateOrderItemModifierParams{
		OrderItemID:         modifier.OrderItemID,
		Amount:              modifier.Amount,
		ModifierGroupName:   modifier.ModifierGroupName,
		ModifierID:          modifier.ModifierID,
		ModifierOptionID:    modifier.ModifierOptionID,
		ModifierOptionName:  modifier.ModifierOptionName,
		ParentModifierID:    modifier.ParentModifierID,
		Quantity:            modifier.Quantity,
		SubParentModifierID: modifier.SubParentModifierID,
		CreatedAt:           modifier.CreatedAt,
		UpdatedAt:           modifier.UpdatedAt,
	}
}

func OrderPaymentsToDgPosPaymentsParams(orderID int32, OrderPayment *db.DgPosPayment) *db.CreateDgPosPaymentsParams {
	return &db.CreateDgPosPaymentsParams{
		OrderID:              orderID,
		Amount:               OrderPayment.Amount,
		Cash:                 OrderPayment.Cash,
		Balance:              OrderPayment.Balance,
		DateTime:             OrderPayment.DateTime,
		PaymentMethodID:      OrderPayment.PaymentMethodID,
		TransactionID:        OrderPayment.TransactionID,
		CreatedAt:            OrderPayment.CreatedAt,
		UpdatedAt:            OrderPayment.UpdatedAt,
		Status:               OrderPayment.Status,
		RefundID:             OrderPayment.RefundID,
		IsRefund:             OrderPayment.IsRefund,
		PaymentType:          OrderPayment.PaymentType,
		CardTransactionToken: OrderPayment.CardTransactionToken,
	}
}

func OrderToOrderUpdateParams(order *Order) *db.UpdateOrderParams {
	return &db.UpdateOrderParams{
		ID:                        order.ID,
		CustomerID:                order.CustomerID,
		CustomerName:              order.CustomerName,
		DeliveryDateTime:          order.DeliveryDateTime,
		DeliveryPlatformID:        order.PlatformID,
		Discount:                  order.Discount,
		VoucherDiscount:           order.VoucherDiscount,
		DiscountModeApplied:       order.DiscountModeApplied,
		DiscountPercentageApplied: order.DiscountPercentageApplied,
		DiscountType:              order.DiscountType,
		DisplayOrderID:            order.DisplayOrderID,
		Note:                      order.Note,
		PaymentMethod:             order.PaymentMethod,
		RemoteOrderID:             order.RemoteOrderID,
		ShippingMethod:            order.ShippingMethod,
		ShippingTax:               order.ShippingTax,
		ShippingTotal:             order.ShippingTotal,
		ShopID:                    order.ShopID,
		SubTotal:                  order.SubTotal,
		DgposTableID:              order.DgposTableID,
		Tip:                       order.Tip,
		TipPercentage:             order.TipPercentage,
		TotalAmount:               order.TotalAmount,
		TotalTax:                  order.TotalTax,
		UserID:                    order.UserID,
		CancelledByCustomer:       order.CancelledByCustomer,
		CashDue:                   order.CashDue,
		DevicePlatform:            order.DevicePlatform,
		IsScheduled:               order.IsScheduled,
		IsTableOrder:              order.IsTableOrder,
		OrderDelayed:              order.OrderDelayed,
		OrderTypeID:               order.OrderTypeID,
		Status:                    order.Status,
		Surcharge:                 order.Surcharge,
		TestingOrder:              order.TestingOrder,
		Vouchers:                  order.Vouchers,
		CampaignCode:              order.CampaignCode,
		CancelledReason:           order.CancelledReason,
		ContactAccessCode:         order.ContactAccessCode,
		DeliveryLocationID:        order.DeliveryLocationID,
		TableOrderMethodID:        order.TableOrderMethodID,
		TotalFee:                  order.TotalFee,
		UniqueOrderID:             order.UniqueOrderID,
	}
}

func OrderCommissionToOrderCommissionParams(orderID int64, orderCommission *db.OrderCommission) *db.CreateOrderCommissionParams {
	return &db.CreateOrderCommissionParams{
		OrderTmpID: orderCommission.OrderTmpID,
		OrderID:    orderID,
		Commission: orderCommission.Commission,
		Reference:  orderCommission.Reference,
		ReportID:   orderCommission.ReportID,
		Status:     orderCommission.Status,
		CreatedAt:  orderCommission.CreatedAt,
		UpdatedAt:  orderCommission.UpdatedAt,
	}
}

func OrderLocationToOrderLocationParams(orderID int32, orderLocation *db.DgPosOrderLocation) *db.CreateDgPosOrderLocationParams {
	return &db.CreateDgPosOrderLocationParams{
		OrderID:      orderID,
		FirstName:    orderLocation.FirstName,
		LastName:     orderLocation.LastName,
		Email:        orderLocation.Email,
		CountryCode:  orderLocation.CountryCode,
		Country:      orderLocation.Country,
		Phone:        orderLocation.Phone,
		FlatNo:       orderLocation.FlatNo,
		HouseNo:      orderLocation.HouseNo,
		AddressLine1: orderLocation.AddressLine1,
		AddressLine2: orderLocation.AddressLine2,
		City:         orderLocation.City,
		Landmark:     orderLocation.Landmark,
		Postcode:     orderLocation.Postcode,
		Latitude:     orderLocation.Latitude,
		Longitude:    orderLocation.Longitude,
	}
}

func OrderTimestampsToOrderTimestampsParams(orderID int32, orderTimestamps *db.DgPosOrderStatusTimestamp) *db.UpdateOrderTimestampsParams {
	return &db.UpdateOrderTimestampsParams{
		OrderID:   orderID,
		Queue:     orderTimestamps.Queue,
		Preparing: orderTimestamps.Preparing,
		Ready:     orderTimestamps.Ready,
		Served:    orderTimestamps.Served,
		Delivered: orderTimestamps.Delivered,
		Completed: orderTimestamps.Completed,
		UpdatedAt: orderTimestamps.UpdatedAt,
	}
}
