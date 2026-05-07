package convert

import (
	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

func DBCustomerToCustomerResp(dbCustomer *db.Customer) *types.GetCustomersResponse {
	return &types.GetCustomersResponse{
		ID:          dbCustomer.ID,
		FirstName:   dbCustomer.FirstName.String,
		LastName:    dbCustomer.LastName.String,
		CountryCode: dbCustomer.CountryCode.String,
		Phone:       dbCustomer.Phone.String,
		Addresses:   []*types.CustomerAddress{},
	}
}

func CustomersWithAddressesRow(row *db.GetCustomersWithAddressesRow) *types.CustomerAddress {
	return &types.CustomerAddress{
		ID:             row.ID.Int64,
		Label:          row.Label.String,
		FlatNo:         row.FlatNo.String,
		HouseNo:        row.HouseNo.String,
		AddressLine1:   row.AddressLine1.String,
		AddressLine2:   row.AddressLine2.String,
		Latitude:       row.Latitude.String,
		Longitude:      row.Longitude.String,
		City:           row.City.String,
		Landmark:       row.Landmark.String,
		PostalCode:     row.PostalCode.String,
		DefaultAddress: row.DefaultAddress.Bool,
	}
}

func DBDeliveryLocationToCustomerAddress(row *db.DeliveryLocation) *types.CustomerAddress {
	return &types.CustomerAddress{
		ID:             int64(row.ID),
		Label:          row.Label.String,
		FlatNo:         row.FlatNo.String,
		HouseNo:        row.HouseNo.String,
		AddressLine1:   row.AddressLine1,
		AddressLine2:   row.AddressLine2.String,
		Latitude:       row.Latitude.String,
		Longitude:      row.Longitude.String,
		City:           row.City.String,
		Landmark:       row.Landmark.String,
		PostalCode:     row.PostalCode.String,
		DefaultAddress: row.DefaultAddress,
	}
}

func CustomerOrdersRowToGetOrdersResponse(dbCustomerOrders []*db.GetCustomerOrdersRow) []*types.GetOrdersResponse {
	orderResponses := make([]*types.GetOrdersResponse, len(dbCustomerOrders))
	for i, row := range dbCustomerOrders {
		orderResponses[i] = toGetOrdersResponse(row)
	}
	return orderResponses
}

func DBCustomerToCustomerDetailsResp(
	dbCustomer *db.Customer,
	dbCustomerAddresses []*db.DeliveryLocation,
	dbCustomerOrders []*db.GetCustomerOrdersRow,
) *types.GetCustomerDetailsResponse {

	customer := DBCustomerToCustomerResp(dbCustomer)
	for _, address := range dbCustomerAddresses {
		customer.Addresses = append(customer.Addresses, DBDeliveryLocationToCustomerAddress(address))
	}

	return &types.GetCustomerDetailsResponse{
		GetCustomersResponse: customer,
		Orders:               CustomerOrdersRowToGetOrdersResponse(dbCustomerOrders),
	}
}

func toGetOrdersResponse(row *db.GetCustomerOrdersRow) *types.GetOrdersResponse {
	return &types.GetOrdersResponse{
		ID:                  row.Order.ID,
		ShopID:              row.Order.ShopID,
		DeliveryPlatformID:  row.Order.PlatformID,
		PlatformID:          row.DpID.Int32,
		PlatformName:        row.PlatformName.String,
		PlatformLogo:        row.PlatformLogo.String,
		RemoteOrderID:       row.Order.RemoteOrderID,
		DisplayOrderID:      row.Order.DisplayOrderID.String,
		DeliveryDateTime:    row.Order.DeliveryDateTime,
		TotalAmount:         parseAmount(row.Order.TotalAmount),
		SubTotal:            parseAmount(row.Order.SubTotal.Int32),
		TotalFee:            parseAmount(row.Order.TotalFee.Int32),
		CampaignCode:        row.Order.CampaignCode.String,
		Discount:            parseAmount(row.Order.Discount),
		DiscountType:        row.Order.DiscountType.String,
		Vouchers:            row.Order.Vouchers.String,
		Status:              row.Order.Status,
		CancelledReason:     row.Order.CancelledReason.String,
		OrderTypeID:         row.Order.OrderTypeID.Int32,
		Note:                row.Order.Note.String,
		CreatedAt:           row.Order.CreatedAt.Time,
		UpdatedAt:           row.Order.UpdatedAt.Time,
		CustomerID:          row.Order.CustomerID.Int32,
		CustomerName:        row.Order.CustomerName.String,
		UserID:              row.Order.UserID.Int32,
		DeliveryLocationID:  row.Order.DeliveryLocationID.Int32,
		ShippingMethod:      row.Order.ShippingMethod.String,
		ShippingTotal:       parseFloat(row.Order.ShippingTotal.String),
		ShippingTax:         parseFloat(row.Order.ShippingTax.String),
		TotalTax:            parseFloat(row.Order.TotalTax.String),
		CashDue:             row.Order.CashDue,
		Surcharge:           parseFloat(row.Order.Surcharge),
		ContactAccessCode:   row.Order.ContactAccessCode.String,
		TestingOrder:        row.Order.TestingOrder,
		CancelledByCustomer: row.Order.CancelledByCustomer,
		PaymentMethod:       row.Order.PaymentMethod.String,
		IsScheduled:         row.Order.IsScheduled,
		IsTableOrder:        row.Order.IsTableOrder,
		Tip:                 row.Order.Tip,
		TipPercentage:       row.Order.TipPercentage,
		TableID:             row.Order.DgposTableID.Int32,
		TableOrderMethodID:  row.Order.TableOrderMethodID.Int32,
		DevicePlatform:      row.Order.DevicePlatform.String,
		OrderDelayed:        row.Order.OrderDelayed,
		UniqueOrderID:       row.Order.UniqueOrderID.String,
	}
}
