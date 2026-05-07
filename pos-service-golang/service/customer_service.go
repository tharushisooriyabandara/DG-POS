package service

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"regexp"
	"sort"
	"strings"
	"unicode"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	posErr "github.com/Delivergate-Dev/pos-service-golang/errors"
	"github.com/Delivergate-Dev/pos-service-golang/logger"
	"github.com/Delivergate-Dev/pos-service-golang/service/convert"
	"github.com/Delivergate-Dev/pos-service-golang/service/crypt"
	"github.com/Delivergate-Dev/pos-service-golang/service/generate"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"go.uber.org/zap"
)

type customerCryptoService interface {
	EncryptCustomerCreateParams(ctx context.Context, customer *db.CreateCustomerParams) (*db.CreateCustomerParams, error)
	EncryptCustomerUpdateParams(ctx context.Context, customer *db.UpdateCustomerParams) (*db.UpdateCustomerParams, error)
	DecryptCustomer(ctx context.Context, customer *db.Customer) (*db.Customer, error)
	DecryptCustomerFunc(ctx context.Context) (func(*db.Customer) (*db.Customer, error), error)
}

var (
	ErrCustomerAlreadyExists = errors.New("customer already exists")
	ErrCustomerCreate        = errors.New("error in creating customer")
)

type CustomerService struct {
	db     *sql.DB
	logger *zap.Logger
	crypto customerCryptoService
}

func NewCustomerService(db *sql.DB, logger *zap.Logger, crypto customerCryptoService) *CustomerService {
	return &CustomerService{
		logger: logger,
		db:     db,
		crypto: crypto,
	}
}

func (s *CustomerService) GetCustomerDetails(ctx context.Context, customerID uint64) (*types.GetCustomerDetailsResponse, error) {
	queries := db.New(s.db)

	customer, err := queries.GetCustomerByID(ctx, customerID)
	if err != nil {
		return nil, fmt.Errorf("failed to get customer: %w", err)
	}

	customerAddresses, err := queries.GetCustomerAddresses(ctx, int32(customerID))
	if err != nil {
		return nil, fmt.Errorf("failed to get customer addresses: %w", err)
	}

	customerOrders, err := queries.GetCustomerOrders(ctx, sql.NullInt32{Int32: int32(customerID), Valid: true})
	if err != nil {
		return nil, fmt.Errorf("failed to get customer orders: %w", err)
	}

	decryptedCustomer, err := s.crypto.DecryptCustomer(ctx, customer)
	if err != nil {
		return nil, fmt.Errorf("failed to decrypt customer: %w", err)
	}

	return convert.DBCustomerToCustomerDetailsResp(decryptedCustomer, customerAddresses, customerOrders), nil
}

func (s *CustomerService) GetCustomers(ctx context.Context) ([]*types.GetCustomersResponse, error) {
	queries := db.New(s.db)

	rows, err := queries.GetCustomersWithAddresses(ctx)
	if err != nil {
		return nil, fmt.Errorf("failed to get customers: %w", err)
	}

	decryptCustomerFunc, err := s.crypto.DecryptCustomerFunc(ctx)
	if err != nil {
		return nil, fmt.Errorf("failed to get decrypt customer func: %w", err)
	}

	for _, row := range rows {
		decryptedCustomer, err := decryptCustomerFunc(&row.Customer)
		if err != nil {
			logger.Error("Failed to decrypt customer", zap.Error(err))
			continue
		}
		row.Customer = *decryptedCustomer
	}

	// fold rows by customer id, and add delivery addresses to the customer.
	// this depends on the query returning customers sorted by customer
	customers := make([]*types.GetCustomersResponse, 0, len(rows))
	customerMap := make(map[uint64]*types.GetCustomersResponse, len(rows))

	for _, row := range rows {
		customer, exists := customerMap[row.Customer.ID]
		if !exists {
			customer = convert.DBCustomerToCustomerResp(&row.Customer)
			customers = append(customers, customer)
			customerMap[row.Customer.ID] = customer
		}

		if row.ID.Valid && row.AddressLine1.Valid && row.Label.Valid {
			addr := convert.CustomersWithAddressesRow(row)
			customer.Addresses = append(customer.Addresses, addr)
		}
	}

	// guest customer should be the first customer in the list
	guestCustomer, err := queries.GetGuestCustomer(ctx)
	if err != nil {
		return nil, fmt.Errorf("failed to get guest customer: %w", err)
	}

	var guestCustomerIdx int
	for i, customer := range customers {
		if customer.ID == guestCustomer.ID {
			guestCustomerIdx = i
			break
		}
	}
	customers[guestCustomerIdx], customers[0] = customers[0], customers[guestCustomerIdx]

	// sort alphabetically by first name, still keep the guest customer at the top
	orderByFirstName(customers[1:])

	return customers, nil
}

func orderByFirstName(customers []*types.GetCustomersResponse) {
	sort.Slice(customers, func(i, j int) bool {
		ci := customers[i]
		cj := customers[j]

		// helper to check if first rune is a letter
		isLetter := func(s string) bool {
			for _, r := range s {
				return unicode.IsLetter(r) // just the first rune
			}
			return false
		}

		li, lj := isLetter(ci.FirstName), isLetter(cj.FirstName)

		// if one starts with a letter and the other doesn’t
		if li && !lj {
			return true
		}
		if !li && lj {
			return false
		}

		// both same "class", fall back to regular comparison
		return ci.FirstName < cj.FirstName
	})
}

func (s *CustomerService) CreateCustomer(ctx context.Context, createCustomerRequest *types.CreateCustomerRequest) (*types.GetCustomersResponse, error) {
	// country code check
	if err := checkCountryCode(createCustomerRequest.CountryCode); err != nil {
		return nil, fmt.Errorf("%w : %s", ErrCustomerCreate, err.Error())
	}

	queries := db.New(s.db)

	if existingCustomer, err := checkDuplicatePhoneNumbers(ctx, queries, createCustomerRequest.CountryCode, createCustomerRequest.PhoneNumber); err != nil {
		return nil, err

	} else if existingCustomer.ID != 0 {

		decryptedCustomer, err := s.crypto.DecryptCustomer(ctx, existingCustomer)
		if err != nil {
			return nil, err
		}

		return nil, posErr.NewIncorrectInputError(
			"Customer already exists",
			fmt.Sprintf(
				"This phone number is already registered to %s. Please use a different number.",
				strings.Join([]string{decryptedCustomer.FirstName.String, decryptedCustomer.LastName.String}, " "),
			),
		)
	}

	generated := generate.Name(createCustomerRequest.Name)
	params := &db.CreateCustomerParams{
		FirstName:       sql.NullString{String: generated.FirstName, Valid: true},
		HashedFirstName: sql.NullString{String: crypt.ToHashed(generated.FirstName), Valid: true},
		LastName:        sql.NullString{String: generated.LastName, Valid: generated.LastName != ""},
		HashedLastName:  sql.NullString{String: crypt.ToHashed(generated.LastName), Valid: generated.LastName != ""},
		Phone:           sql.NullString{String: createCustomerRequest.PhoneNumber, Valid: true},
		HashedPhone:     sql.NullString{String: crypt.ToHashed(createCustomerRequest.PhoneNumber), Valid: true},
		CountryCode:     sql.NullString{String: createCustomerRequest.CountryCode, Valid: true},
		Type:            sql.NullString{String: "DG_POS", Valid: true},
		DevicePlatform:  sql.NullString{String: "dg_pos", Valid: true},
		Status:          true,
		AccountBrand:    sql.NullInt32{Int32: createCustomerRequest.Requestor.BrandID, Valid: true},
	}

	encryptedParams, err := s.crypto.EncryptCustomerCreateParams(ctx, params)
	if err != nil {
		return nil, err
	}

	newCustomerID, err := queries.CreateCustomer(ctx, encryptedParams)
	if err != nil {
		return nil, fmt.Errorf("failed to create customer: %w", err)
	}

	customer, err := queries.GetCustomerByID(ctx, uint64(newCustomerID))
	if err != nil {
		return nil, fmt.Errorf("failed to get customer: %w", err)
	}

	decryptedCustomer, err := s.crypto.DecryptCustomer(ctx, customer)
	if err != nil {
		return nil, err
	}

	return convert.DBCustomerToCustomerResp(decryptedCustomer), nil
}

func (s *CustomerService) CreateCustomerAddress(ctx context.Context, createCustomerAddressRequest *types.CreateCustomerAddressRequest) error {
	queries := db.New(s.db)

	// guest customer shouldn't be edited
	guest, err := queries.GetGuestCustomer(ctx)
	if err != nil {
		return fmt.Errorf("failed to get guest customer: %w", err)
	}

	if uint64(createCustomerAddressRequest.CustomerID) == guest.ID {
		return fmt.Errorf("guest customer cannot be edited")
	}

	// if the address is default, make all other addresses non-default
	if createCustomerAddressRequest.DefaultAddress {
		if err := queries.MakeCustomerAddressNonDefault(ctx, createCustomerAddressRequest.CustomerID); err != nil {
			return fmt.Errorf("failed to make customer address non-default: %w", err)
		}
	}

	_, err = queries.CreateDeliveryLocation(ctx, &db.CreateDeliveryLocationParams{
		CustomerID:     createCustomerAddressRequest.CustomerID,
		Label:          sql.NullString{String: createCustomerAddressRequest.Label, Valid: true},
		FlatNo:         sql.NullString{String: createCustomerAddressRequest.FlatNo, Valid: createCustomerAddressRequest.FlatNo != ""},
		HouseNo:        sql.NullString{String: createCustomerAddressRequest.HouseNo, Valid: createCustomerAddressRequest.HouseNo != ""},
		AddressLine1:   createCustomerAddressRequest.AddressLine1,
		AddressLine2:   sql.NullString{String: createCustomerAddressRequest.AddressLine2, Valid: createCustomerAddressRequest.AddressLine2 != ""},
		Latitude:       sql.NullString{String: createCustomerAddressRequest.Latitude, Valid: createCustomerAddressRequest.Latitude != ""},
		Longitude:      sql.NullString{String: createCustomerAddressRequest.Longitude, Valid: createCustomerAddressRequest.Longitude != ""},
		City:           sql.NullString{String: createCustomerAddressRequest.City, Valid: createCustomerAddressRequest.City != ""},
		Landmark:       sql.NullString{String: createCustomerAddressRequest.Landmark, Valid: createCustomerAddressRequest.Landmark != ""},
		PostalCode:     sql.NullString{String: createCustomerAddressRequest.PostalCode, Valid: createCustomerAddressRequest.PostalCode != ""},
		DefaultAddress: createCustomerAddressRequest.DefaultAddress,
	})
	if err != nil {
		return fmt.Errorf("failed to create customer address: %w", err)
	}

	return nil

}

func (s *CustomerService) UpdateCustomer(ctx context.Context, updateCustomerRequest *types.UpdateCustomerRequest) error {
	queries := db.New(s.db)

	// guest customer shouldn't be edited
	guest, err := queries.GetGuestCustomer(ctx)
	if err != nil {
		return fmt.Errorf("failed to get guest customer: %w", err)
	}

	if updateCustomerRequest.ID == guest.ID {
		return fmt.Errorf("guest customer cannot be edited")
	}

	// country code check
	if err := checkCountryCode(updateCustomerRequest.CountryCode); err != nil {
		return fmt.Errorf("%w : %s", ErrCustomerCreate, err.Error())
	}

	if existingCustomer, err := checkDuplicatePhoneNumbers(ctx, queries, updateCustomerRequest.CountryCode, updateCustomerRequest.Phone); err != nil {
		return err
	} else if existingCustomer.ID != 0 && existingCustomer.ID != updateCustomerRequest.ID {

		decryptedCustomer, err := s.crypto.DecryptCustomer(ctx, existingCustomer)
		if err != nil {
			return err
		}

		return posErr.NewIncorrectInputError(
			"Customer already exists",
			fmt.Sprintf(
				"This phone number is already registered to %s. Please use a different number.",
				strings.Join([]string{decryptedCustomer.FirstName.String, decryptedCustomer.LastName.String}, " "),
			),
		)
	}

	// check if there are more than one default address, and if so, make the first one default
	if err := makeFirstFoundDefaultAddressDefault(updateCustomerRequest); err != nil {
		return err
	}

	// check if any old addresses are removed, and delete them
	if err := deleteRemovedAddresses(ctx, queries, updateCustomerRequest); err != nil {
		return err
	}

	// update customer
	params := &db.UpdateCustomerParams{
		ID:              updateCustomerRequest.ID,
		FirstName:       sql.NullString{String: updateCustomerRequest.FirstName, Valid: true},
		HashedFirstName: sql.NullString{String: crypt.ToHashed(updateCustomerRequest.FirstName), Valid: true},
		LastName:        sql.NullString{String: updateCustomerRequest.LastName, Valid: true},
		HashedLastName:  sql.NullString{String: crypt.ToHashed(updateCustomerRequest.LastName), Valid: true},
		Phone:           sql.NullString{String: updateCustomerRequest.Phone, Valid: true},
		HashedPhone:     sql.NullString{String: crypt.ToHashed(updateCustomerRequest.Phone), Valid: true},
		CountryCode:     sql.NullString{String: updateCustomerRequest.CountryCode, Valid: true},
	}

	encryptedParams, err := s.crypto.EncryptCustomerUpdateParams(ctx, params)
	if err != nil {
		return err
	}

	if err := queries.UpdateCustomer(ctx, encryptedParams); err != nil {
		return fmt.Errorf("failed to update customer: %w", err)
	}

	// update addresses
	for _, address := range updateCustomerRequest.Addresses {
		if err := queries.UpdateDeliveryLocation(ctx, &db.UpdateDeliveryLocationParams{
			ID:             uint64(address.ID),
			CustomerID:     int32(updateCustomerRequest.ID),
			Label:          sql.NullString{String: address.Label, Valid: true},
			FlatNo:         sql.NullString{String: address.FlatNo, Valid: address.FlatNo != ""},
			HouseNo:        sql.NullString{String: address.HouseNo, Valid: address.HouseNo != ""},
			AddressLine1:   address.AddressLine1,
			AddressLine2:   sql.NullString{String: address.AddressLine2, Valid: address.AddressLine2 != ""},
			Latitude:       sql.NullString{String: address.Latitude, Valid: address.Latitude != ""},
			Longitude:      sql.NullString{String: address.Longitude, Valid: address.Longitude != ""},
			City:           sql.NullString{String: address.City, Valid: address.City != ""},
			Landmark:       sql.NullString{String: address.Landmark, Valid: address.Landmark != ""},
			PostalCode:     sql.NullString{String: address.PostalCode, Valid: address.PostalCode != ""},
			DefaultAddress: address.DefaultAddress,
		}); err != nil {
			return fmt.Errorf("failed to update customer address: %w", err)
		}
	}

	return nil
}

func makeFirstFoundDefaultAddressDefault(updateCustomerRequest *types.UpdateCustomerRequest) error {
	defaultAddressCount := 0
	for _, address := range updateCustomerRequest.Addresses {
		if address.DefaultAddress {
			defaultAddressCount++
			if defaultAddressCount > 1 {
				address.DefaultAddress = false
			}
		}
	}

	return nil
}

func deleteRemovedAddresses(ctx context.Context, queries *db.Queries, updateCustomerRequest *types.UpdateCustomerRequest) error {

	existingAddresses, err := queries.GetCustomerAddresses(ctx, int32(updateCustomerRequest.ID))
	if err != nil {
		return fmt.Errorf("failed to get customer addresses: %w", err)
	}

	existingAddressMap := make(map[uint64]*db.DeliveryLocation, len(existingAddresses))
	for _, address := range existingAddresses {
		existingAddressMap[address.ID] = address
	}

	for _, address := range updateCustomerRequest.Addresses {
		delete(existingAddressMap, uint64(address.ID))
	}

	for _, address := range existingAddressMap {
		if err := queries.DeleteDeliveryLocation(ctx, address.ID); err != nil {
			return fmt.Errorf("failed to delete customer address: %w", err)
		}
	}

	return nil
}

func checkDuplicatePhoneNumbers(ctx context.Context, queries *db.Queries, countryCode, phoneNumber string) (*db.Customer, error) {
	if phoneNumber[0] == '0' {
		phoneNumber = phoneNumber[1:]
	}

	existingCustomer, err := queries.GetCustomerByPhone(ctx, &db.GetCustomerByPhoneParams{
		PhoneNumberFormats: possiblePhoneNumberFormats(countryCode, phoneNumber),
		CountryCode:        sql.NullString{String: countryCode, Valid: true},
	})
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return nil, fmt.Errorf("failed to check if customer exists: %w", err)
	}

	return existingCustomer, nil
}

func possiblePhoneNumberFormats(countryCode, original string) []sql.NullString {
	formatA := fmt.Sprintf("0%s", original)                    // 0769130401
	formatB := fmt.Sprintf("+%s%s", countryCode, original[1:]) // +94769130401
	formatC := fmt.Sprintf("+%s0%s", countryCode, original)    // +940769130401

	return []sql.NullString{
		{String: crypt.ToHashed(original), Valid: true},
		{String: crypt.ToHashed(formatA), Valid: true},
		{String: crypt.ToHashed(formatB), Valid: true},
		{String: crypt.ToHashed(formatC), Valid: true},
	}
}

func checkCountryCode(code string) error {
	// country code check
	if ok, err := regexp.MatchString(`^\+\d{1,3}$`, code); err != nil {
		return err
	} else if !ok {
		return fmt.Errorf("invalid country code: %s", code)
	}

	return nil
}
