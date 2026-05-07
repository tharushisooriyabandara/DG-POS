package convert

import (
	"encoding/json"
	"strconv"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/service/generate"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

func getOrdersRowToGetOrdersResponse(row *db.GetOrdersRow) *types.GetOrdersResponse {
	return &types.GetOrdersResponse{
		ID:                   row.Order.ID,
		ShopID:               row.Order.ShopID,
		DeliveryPlatformID:   row.Order.PlatformID,
		DeliveryPlatformName: row.DeliveryPlatformName,
		PlatformID:           row.DpID.Int32,
		PlatformName:         row.PlatformName.String,
		PlatformLogo:         row.PlatformLogo.String,
		RemoteOrderID:        row.Order.RemoteOrderID,
		DisplayOrderID:       row.Order.DisplayOrderID.String,
		DeliveryDateTime:     row.Order.DeliveryDateTime,
		TotalAmount:          parseAmount(row.Order.TotalAmount),
		PaymentStatus:        row.PaymentStatus,
		SubTotal:             parseAmount(row.Order.SubTotal.Int32),
		TotalFee:             parseAmount(row.Order.TotalFee.Int32),
		CampaignCode:         row.Order.CampaignCode.String,
		Discount:             parseAmount(row.Order.Discount),
		DiscountModeApplied:  row.Order.DiscountModeApplied.String,
		DiscountPercentage:   parseAmount(row.Order.DiscountPercentageApplied.Int32),
		DiscountType:         row.Order.DiscountType.String,
		Vouchers:             row.Order.Vouchers.String,
		Status:               row.Order.Status,
		CancelledReason:      row.Order.CancelledReason.String,
		OrderTypeID:          row.Order.OrderTypeID.Int32,
		Note:                 row.Order.Note.String,
		CreatedAt:            row.Order.CreatedAt.Time,
		UpdatedAt:            row.Order.UpdatedAt.Time,
		CustomerID:           row.Order.CustomerID.Int32,
		CustomerName:         row.Order.CustomerName.String,
		UserID:               row.Order.UserID.Int32,
		DeliveryLocationID:   row.Order.DeliveryLocationID.Int32,
		ShippingMethod:       row.Order.ShippingMethod.String,
		ShippingTotal:        parseFloat(row.Order.ShippingTotal.String),
		ShippingTax:          parseFloat(row.Order.ShippingTax.String),
		TotalTax:             parseFloat(row.Order.TotalTax.String),
		CashDue:              row.Order.CashDue,
		Surcharge:            parseFloat(row.Order.Surcharge),
		ContactAccessCode:    row.Order.ContactAccessCode.String,
		TestingOrder:         row.Order.TestingOrder,
		CancelledByCustomer:  row.Order.CancelledByCustomer,
		PaymentMethod:        row.Order.PaymentMethod.String,
		PaymentMode:          row.Order.PaymentMode.String,
		IsScheduled:          row.Order.IsScheduled,
		IsTableOrder:         row.Order.IsTableOrder,
		Tip:                  row.Order.Tip,
		TipPercentage:        row.Order.TipPercentage,
		TableID:              row.Order.DgposTableID.Int32,
		TableName:            row.TableName.String,
		TableOrderingsID:     row.TableOrderingsID.Int64,
		TableOrderMethodID:   row.Order.TableOrderMethodID.Int32,
		TableOrderMethod:     row.TableOrderMethodName.String,
		DevicePlatform:       row.Order.DevicePlatform.String,
		OrderDelayed:         row.Order.OrderDelayed,
		UniqueOrderID:        row.Order.UniqueOrderID.String,
		CashDrawerSessionID:  int64(row.CashDrawerSessionID),
		OrderSessionID:       row.Order.OrderSessionID.Int32,
	}
}

func DbOrdersToGetOrdersResponse(dbOrders []*db.GetOrdersRow) []*types.GetOrdersResponse {
	orderResponses := make([]*types.GetOrdersResponse, len(dbOrders))
	for i, row := range dbOrders {
		orderResponses[i] = getOrdersRowToGetOrdersResponse(row)
	}
	return orderResponses
}

func DbTablesRowToGetTablesResponse(tables []*db.DgPosTable) []*types.GetTablesResponse {
	tableResponses := make([]*types.GetTablesResponse, len(tables))
	for i, table := range tables {
		tableResponses[i] = &types.GetTablesResponse{
			ID:               table.ID,
			ShopID:           table.ShopID,
			BrandID:          table.BrandID,
			Name:             table.Name,
			Description:      table.Description.String,
			SeatCount:        table.SeatCount,
			Status:           table.Status,
			TableOrderingsID: table.TableOrderingsID.Int64,
			CreatedAt:        table.CreatedAt.Time,
			UpdatedAt:        table.UpdatedAt.Time,
		}
	}
	return tableResponses
}

func ToTableOngoingOrdersResponse(orders []*db.GetOngoingOrdersForTableRow) []*types.TableOngoingOrdersResponse {
	orderResponses := make([]*types.TableOngoingOrdersResponse, len(orders))
	for i, order := range orders {
		orderResponses[i] = &types.TableOngoingOrdersResponse{
			OrderID:          order.OrderID,
			DisplayOrderID:   order.DisplayOrderID.String,
			Status:           order.Status,
			TotalAmount:      parseAmount(order.TotalAmount),
			CustomerName:     order.CustomerName.String,
			OrderSessionID:   order.OrderSessionID.Int32,
			IsTableOrder:     order.IsTableOrder,
			PaymentStatus:    order.PaymentStatus,
			ShippingMethod:   order.ShippingMethod.String,
			TableOrderMethod: order.TableOrderMethodName.String,
			PlatformLogo:     order.PlatformLogo.String,
			CreatedAt:        order.CreatedAt.Time,
		}
	}
	return orderResponses
}
func DbOrderItemsToGetOrderItemsResponse(orderItems []*db.GetOrderItemsByOrderIDRow, getPrinterGroupsForItem func(itemID int32) ([]*types.PrinterGroupsResp, error)) ([]*types.GetOrderItemsResponse, error) {
	items := make([]*types.GetOrderItemsResponse, len(orderItems))
	for i, item := range orderItems {

		var modifiers json.RawMessage
		if item.Modifiers.Valid {
			var err error
			modifiers, err = generate.PhpUnserialize(item.Modifiers.String)
			if err != nil {
				return nil, err
			}
		}

		var td *types.TaxDetailsResponse
		if item.TaxID.Valid {
			td = &types.TaxDetailsResponse{
				OrderItemTaxID: item.OrderItemTaxID.Int64,
				TaxProfileID:   item.TaxProfileID.Int32,
				TaxRuleID:      item.TaxRuleID.Int32,
				TaxID:          item.TaxID.Int32,
				TaxCode:        item.TaxCode.String,
				TaxRate:        parseFloat(item.TaxRate.String),
				Amount:         float64(item.TaxAmount.Int32) / 100.00,
			}
		}

		printerGroups, err := getPrinterGroupsForItem(parseInt(item.ItemID))
		if err != nil {
			return nil, err
		}

		items[i] = &types.GetOrderItemsResponse{
			ID:             item.ID,
			OrderID:        item.OrderID,
			ItemID:         parseInt(item.ItemID),
			Quantity:       item.Quantity,
			PricePerItem:   item.PricePerItem,
			Total:          item.Total,
			OriginalPrice:  item.OriginalPrice,
			IsSale:         item.IsSale,
			DiscountAmount: item.DiscountAmount,
			Status:         item.Status,
			CreatedAt:      item.CreatedAt.Time,
			UpdatedAt:      item.UpdatedAt.Time,
			ItemName:       item.ItemName,
			CategoryName:   item.CategoryName.String,
			Tax:            item.Tax,
			Note:           item.Note.String,
			PrinterGroups:  printerGroups,
			TaxDetails:     td,
			Modifiers:      modifiers,
		}
	}
	return items, nil
}

func DbOrderToGetOrderResponse(row *db.GetOrderByIdRow) *types.GetOrderResponse {
	var vouchers []types.OrderVoucherResponse
	phpUnserialized, err := generate.PhpUnserialize(row.Order.Vouchers.String)
	if err == nil {
		err = json.Unmarshal(phpUnserialized, &vouchers)
	}

	tableOrderingsId, _ := strconv.Atoi(row.Order.TableID.String)
	return &types.GetOrderResponse{
		ID:                   row.Order.ID,
		ShopID:               row.Order.ShopID,
		DeliveryPlatformID:   row.Order.PlatformID,
		DeliveryPlatformName: row.DpName.String,
		PlatformID:           row.PlatformID.Int64,
		PlatformName:         row.PlatformName.String,
		PlatformLogo:         row.PlatformLogo.String,
		RemoteOrderID:        row.Order.RemoteOrderID,
		DisplayOrderID:       row.Order.DisplayOrderID.String,
		DeliveryDateTime:     row.Order.DeliveryDateTime,
		TotalAmount:          parseAmount(row.Order.TotalAmount),
		SubTotal:             parseAmount(row.Order.SubTotal.Int32),
		TotalFee:             parseAmount(row.Order.TotalFee.Int32),
		CampaignCode:         row.Order.CampaignCode.String,
		Discount:             parseAmount(row.Order.Discount) - parseAmount(row.Order.VoucherDiscount),
		VoucherDiscount:      parseAmount(row.Order.VoucherDiscount),
		DiscountModeApplied:  row.Order.DiscountModeApplied.String,
		DiscountPercentage:   parseAmount(row.Order.DiscountPercentageApplied.Int32),
		DiscountType:         row.Order.DiscountType.String,
		Vouchers:             vouchers,
		Status:               row.Order.Status,
		CancelledReason:      row.Order.CancelledReason.String,
		OrderTypeID:          row.Order.OrderTypeID.Int32,
		Note:                 row.Order.Note.String,
		CreatedAt:            row.Order.CreatedAt.Time,
		UpdatedAt:            row.Order.UpdatedAt.Time,
		CustomerID:           row.Order.CustomerID.Int32,
		CustomerName:         row.Order.CustomerName.String,
		UserID:               row.Order.UserID.Int32,
		DeliveryLocationID:   row.Order.DeliveryLocationID.Int32,
		ShippingMethod:       row.Order.ShippingMethod.String,
		ShippingTotal:        parseFloat(row.Order.ShippingTotal.String),
		ShippingTax:          parseFloat(row.Order.ShippingTax.String),
		DeliveryTax:          parseAmount(row.Order.DeliveryTax.Int32),
		TaxID:                row.Order.TaxID.Int32,
		TaxRate:              row.DeliveryTaxRate.String,
		TaxCode:              row.DeliveryTaxCode.String,
		TotalTax:             parseFloat(row.Order.TotalTax.String),
		CashDue:              row.Order.CashDue,
		Surcharge:            parseFloat(row.Order.Surcharge),
		ContactAccessCode:    row.Order.ContactAccessCode.String,
		TestingOrder:         row.Order.TestingOrder,
		CancelledByCustomer:  row.Order.CancelledByCustomer,
		PaymentMethod:        row.Order.PaymentMethod.String,
		PaymentMode:          row.Order.PaymentMode.String,
		IsScheduled:          row.Order.IsScheduled,
		IsTableOrder:         row.Order.IsTableOrder,
		Tip:                  row.Order.Tip,
		TipPercentage:        row.Order.TipPercentage,
		TableID:              row.Order.DgposTableID.Int32,
		TableOrderingsID:     int32(tableOrderingsId),
		TableOrderMethodID:   row.Order.TableOrderMethodID.Int32,
		TableOrderMethod:     row.TableOrderMethodName.String,
		DevicePlatform:       row.Order.DevicePlatform.String,
		OrderDelayed:         row.Order.OrderDelayed,
		UniqueOrderID:        row.Order.UniqueOrderID.String,
		UserShiftID:          row.ShiftID,
		OrderSessionID:       row.Order.OrderSessionID.Int32,
		SessionPaymentType:   row.SessionPaymentType.String,
		Items:                nil,
	}
}

func DGShippingDetailsToShippingDetailsResponse(dgOrderLocation *db.DgPosOrderLocation) *types.ShippingDetailsResponse {
	if dgOrderLocation == nil {
		return nil
	}

	return &types.ShippingDetailsResponse{
		ID:           dgOrderLocation.ID,
		OrderID:      int64(dgOrderLocation.OrderID),
		FirstName:    dgOrderLocation.FirstName,
		LastName:     dgOrderLocation.LastName,
		Email:        dgOrderLocation.Email.String,
		CountryCode:  dgOrderLocation.CountryCode,
		Phone:        dgOrderLocation.Phone,
		FlatNo:       dgOrderLocation.FlatNo.String,
		HouseNo:      dgOrderLocation.HouseNo.String,
		AddressLine1: dgOrderLocation.AddressLine1,
		AddressLine2: dgOrderLocation.AddressLine2.String,
		City:         dgOrderLocation.City.String,
		Postcode:     dgOrderLocation.Postcode.String,
		Country:      dgOrderLocation.Country.String,
		Landmark:     dgOrderLocation.Landmark.String,
		Latitude:     dgOrderLocation.Latitude.String,
		Longitude:    dgOrderLocation.Longitude.String,
		OrderTmpID:   0,
		State:        "",
		Type:         "",
		CreatedAt:    dgOrderLocation.CreatedAt.Time,
		UpdatedAt:    dgOrderLocation.UpdatedAt.Time,
	}
}

func WebshopShippingDetailsToShippingDetailsResponse(webshopOrderLocation *db.WebshopOrderLocation) *types.ShippingDetailsResponse {
	if webshopOrderLocation == nil {
		return nil
	}

	return &types.ShippingDetailsResponse{
		ID:           webshopOrderLocation.ID,
		OrderID:      webshopOrderLocation.OrderID.Int64,
		FirstName:    webshopOrderLocation.FirstName,
		LastName:     webshopOrderLocation.LastName,
		Email:        webshopOrderLocation.Email.String,
		CountryCode:  webshopOrderLocation.CountryCode.String,
		Country:      webshopOrderLocation.Country.String,
		Phone:        webshopOrderLocation.Phone.String,
		FlatNo:       "",
		HouseNo:      "",
		AddressLine1: webshopOrderLocation.AddressLine1.String,
		AddressLine2: webshopOrderLocation.AddressLine2.String,
		City:         webshopOrderLocation.City.String,
		Landmark:     "",
		State:        webshopOrderLocation.State.String,
		Postcode:     webshopOrderLocation.Postcode.String,
		OrderTmpID:   webshopOrderLocation.OrderTmpID.Int64,
		Type:         webshopOrderLocation.Type,
		Latitude:     webshopOrderLocation.Latitude.String,
		Longitude:    webshopOrderLocation.Longitude.String,
		CreatedAt:    webshopOrderLocation.CreatedAt.Time,
		UpdatedAt:    webshopOrderLocation.UpdatedAt.Time,
	}
}

func CustomerShippingDetailsToShippingDetailsResponse(customer *db.Customer) *types.ShippingDetailsResponse {
	if customer == nil {
		return nil
	}

	return &types.ShippingDetailsResponse{
		FirstName:    customer.FirstName.String,
		LastName:     customer.LastName.String,
		Email:        customer.Email.String,
		CountryCode:  customer.CountryCode.String,
		Country:      customer.Country.String,
		Phone:        customer.Phone.String,
		FlatNo:       "",
		HouseNo:      "",
		AddressLine1: customer.Address1.String,
		AddressLine2: customer.Address2.String,
		City:         customer.City.String,
		Landmark:     "",
		State:        "",
		Postcode:     customer.Postcode.String,
		Latitude:     customer.Latitude.String,
		Longitude:    customer.Longitude.String,
	}
}

func DbOrderTaxesToOrderTaxesResponse(orderTaxes []*db.OrderTax) []*types.GetOrderTaxesResponse {
	taxes := make([]*types.GetOrderTaxesResponse, len(orderTaxes))
	for i, tax := range orderTaxes {
		taxes[i] = &types.GetOrderTaxesResponse{
			TaxRate:       tax.TaxRate,
			TaxCode:       tax.TaxCode,
			TaxAmount:     parseAmount(tax.TaxAmount),
			TaxableAmount: parseAmount(tax.TaxableAmount),
		}
	}
	return taxes
}

func DbShopFeesToShopFeesResponse(shopFees []*db.GetShopFeesByOrderIDRow) []*types.ShopFeeResponse {
	fees := make([]*types.ShopFeeResponse, len(shopFees))
	for i, fee := range shopFees {
		fees[i] = &types.ShopFeeResponse{
			ShopFeeID:  fee.ShopFeeID,
			FeeType:    fee.FeeType,
			FeeName:    fee.FeeName,
			Amount:     parseAmount(fee.Amount),
			ShopFeeTax: parseAmount(fee.ShopFeeTax.Int32),
			TaxID:      fee.TaxID.Int32,
			TaxCode:    fee.TaxCode.String,
			TaxRate:    fee.TaxRate.String,
		}
	}
	return fees
}

func ToOrderTransactionsresp(transactions []*db.GetOrderTransactionsRow) []*types.OrderTransactionsResponse {
	transactionResponses := make([]*types.OrderTransactionsResponse, len(transactions))
	for i, transaction := range transactions {
		transactionResponses[i] = &types.OrderTransactionsResponse{
			TransactionType:   transaction.OrderTransaction.TransactionType,
			TransactionAmount: parseAmount(transaction.OrderTransaction.TransactionAmount),
			TransactionMode:   transaction.OrderTransaction.TransactionMode.String,
			CreatedAt:         transaction.OrderTransaction.CreatedAt.Time,
			UpdatedAt:         transaction.OrderTransaction.UpdatedAt.Time,
			Reason:            transaction.Reason.String,
		}
	}
	return transactionResponses
}
