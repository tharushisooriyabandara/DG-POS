package crypt

import (
	"context"
	"database/sql"
	"encoding/base64"
	"fmt"

	aesgcm "github.com/Delivergate-Dev/pos-service-golang/cryptography/aes-gcm"
	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
)

type CustomerCryptoService struct {
	queries           *db.Queries
	encryptionEnabled bool
}

func NewCustomerCryptoService(conn *sql.DB, encryptionEnabled bool) *CustomerCryptoService {
	return &CustomerCryptoService{queries: db.New(conn), encryptionEnabled: encryptionEnabled}
}

func (s *CustomerCryptoService) DecryptCustomer(ctx context.Context, customer *db.Customer) (*db.Customer, error) {
	if customer == nil {
		return nil, nil
	}

	encryptedKeys, err := getKeyMap(ctx, s.queries)
	if err != nil {
		return nil, fmt.Errorf("failed to get encryption keys: %w", err)
	}

	decryptedFields, err := aesgcm.DecryptFields(ctx, map[string]aesgcm.EncryptedField{
		"first_name": toEncryptedField(customer.KeyIDFirstName, encryptedKeys[uint64(customer.KeyIDFirstName)], customer.FirstName.String),
		"last_name":  toEncryptedField(customer.KeyIDLastName, encryptedKeys[uint64(customer.KeyIDLastName)], customer.LastName.String),
		"dob":        toEncryptedField(customer.KeyIDDob, encryptedKeys[uint64(customer.KeyIDDob)], customer.Dob.String),
		"address_1":  toEncryptedField(customer.KeyIDAddress1, encryptedKeys[uint64(customer.KeyIDAddress1)], customer.Address1.String),
		"address_2":  toEncryptedField(customer.KeyIDAddress2, encryptedKeys[uint64(customer.KeyIDAddress2)], customer.Address2.String),
		"email":      toEncryptedField(customer.KeyIDEmail, encryptedKeys[uint64(customer.KeyIDEmail)], customer.Email.String),
		"phone":      toEncryptedField(customer.KeyIDPhone, encryptedKeys[uint64(customer.KeyIDPhone)], customer.Phone.String),
		"city":       toEncryptedField(customer.KeyIDCity, encryptedKeys[uint64(customer.KeyIDCity)], customer.City.String),
		"state":      toEncryptedField(customer.KeyIDState, encryptedKeys[uint64(customer.KeyIDState)], customer.State.String),
		"postcode":   toEncryptedField(customer.KeyIDPostcode, encryptedKeys[uint64(customer.KeyIDPostcode)], customer.Postcode.String),
		"country":    toEncryptedField(customer.KeyIDCountry, encryptedKeys[uint64(customer.KeyIDCountry)], customer.Country.String),
		"latitude":   toEncryptedField(customer.KeyIDLatitude, encryptedKeys[uint64(customer.KeyIDLatitude)], customer.Latitude.String),
		"longitude":  toEncryptedField(customer.KeyIDLongitude, encryptedKeys[uint64(customer.KeyIDLongitude)], customer.Longitude.String),
	})
	if err != nil {
		return nil, fmt.Errorf("failed to decrypt customer: %w", err)
	}

	newCustomer := *customer
	newCustomer.FirstName = sql.NullString{String: string(decryptedFields["first_name"]), Valid: customer.FirstName.Valid}
	newCustomer.LastName = sql.NullString{String: string(decryptedFields["last_name"]), Valid: customer.LastName.Valid}
	newCustomer.Dob = sql.NullString{String: string(decryptedFields["dob"]), Valid: customer.Dob.Valid}
	newCustomer.Address1 = sql.NullString{String: string(decryptedFields["address_1"]), Valid: customer.Address1.Valid}
	newCustomer.Address2 = sql.NullString{String: string(decryptedFields["address_2"]), Valid: customer.Address2.Valid}
	newCustomer.Email = sql.NullString{String: string(decryptedFields["email"]), Valid: customer.Email.Valid}
	newCustomer.Phone = sql.NullString{String: string(decryptedFields["phone"]), Valid: customer.Phone.Valid}
	newCustomer.City = sql.NullString{String: string(decryptedFields["city"]), Valid: customer.City.Valid}
	newCustomer.State = sql.NullString{String: string(decryptedFields["state"]), Valid: customer.State.Valid}
	newCustomer.Postcode = sql.NullString{String: string(decryptedFields["postcode"]), Valid: customer.Postcode.Valid}
	newCustomer.Country = sql.NullString{String: string(decryptedFields["country"]), Valid: customer.Country.Valid}
	newCustomer.Latitude = sql.NullString{String: string(decryptedFields["latitude"]), Valid: customer.Latitude.Valid}
	newCustomer.Longitude = sql.NullString{String: string(decryptedFields["longitude"]), Valid: customer.Longitude.Valid}

	return &newCustomer, nil

}

func (s *CustomerCryptoService) DecryptCustomerFunc(ctx context.Context) (func(*db.Customer) (*db.Customer, error), error) {

	encryptedKeys, err := getKeyMap(ctx, s.queries)
	if err != nil {
		return nil, fmt.Errorf("failed to get encryption keys: %w", err)
	}

	return func(customer *db.Customer) (*db.Customer, error) {
		decryptedFields, err := aesgcm.DecryptFields(ctx, map[string]aesgcm.EncryptedField{
			"first_name": toEncryptedField(customer.KeyIDFirstName, encryptedKeys[uint64(customer.KeyIDFirstName)], customer.FirstName.String),
			"last_name":  toEncryptedField(customer.KeyIDLastName, encryptedKeys[uint64(customer.KeyIDLastName)], customer.LastName.String),
			"dob":        toEncryptedField(customer.KeyIDDob, encryptedKeys[uint64(customer.KeyIDDob)], customer.Dob.String),
			"address_1":  toEncryptedField(customer.KeyIDAddress1, encryptedKeys[uint64(customer.KeyIDAddress1)], customer.Address1.String),
			"address_2":  toEncryptedField(customer.KeyIDAddress2, encryptedKeys[uint64(customer.KeyIDAddress2)], customer.Address2.String),
			"email":      toEncryptedField(customer.KeyIDEmail, encryptedKeys[uint64(customer.KeyIDEmail)], customer.Email.String),
			"phone":      toEncryptedField(customer.KeyIDPhone, encryptedKeys[uint64(customer.KeyIDPhone)], customer.Phone.String),
			"city":       toEncryptedField(customer.KeyIDCity, encryptedKeys[uint64(customer.KeyIDCity)], customer.City.String),
			"state":      toEncryptedField(customer.KeyIDState, encryptedKeys[uint64(customer.KeyIDState)], customer.State.String),
			"postcode":   toEncryptedField(customer.KeyIDPostcode, encryptedKeys[uint64(customer.KeyIDPostcode)], customer.Postcode.String),
			"country":    toEncryptedField(customer.KeyIDCountry, encryptedKeys[uint64(customer.KeyIDCountry)], customer.Country.String),
			"latitude":   toEncryptedField(customer.KeyIDLatitude, encryptedKeys[uint64(customer.KeyIDLatitude)], customer.Latitude.String),
			"longitude":  toEncryptedField(customer.KeyIDLongitude, encryptedKeys[uint64(customer.KeyIDLongitude)], customer.Longitude.String),
		})
		if err != nil {
			return nil, fmt.Errorf("failed to decrypt customer: %w", err)
		}

		newCustomer := *customer
		newCustomer.FirstName = sql.NullString{String: string(decryptedFields["first_name"]), Valid: customer.FirstName.Valid}
		newCustomer.LastName = sql.NullString{String: string(decryptedFields["last_name"]), Valid: customer.LastName.Valid}
		newCustomer.Dob = sql.NullString{String: string(decryptedFields["dob"]), Valid: customer.Dob.Valid}
		newCustomer.Address1 = sql.NullString{String: string(decryptedFields["address_1"]), Valid: customer.Address1.Valid}
		newCustomer.Address2 = sql.NullString{String: string(decryptedFields["address_2"]), Valid: customer.Address2.Valid}
		newCustomer.Email = sql.NullString{String: string(decryptedFields["email"]), Valid: customer.Email.Valid}
		newCustomer.Phone = sql.NullString{String: string(decryptedFields["phone"]), Valid: customer.Phone.Valid}
		newCustomer.City = sql.NullString{String: string(decryptedFields["city"]), Valid: customer.City.Valid}
		newCustomer.State = sql.NullString{String: string(decryptedFields["state"]), Valid: customer.State.Valid}
		newCustomer.Postcode = sql.NullString{String: string(decryptedFields["postcode"]), Valid: customer.Postcode.Valid}
		newCustomer.Country = sql.NullString{String: string(decryptedFields["country"]), Valid: customer.Country.Valid}
		newCustomer.Latitude = sql.NullString{String: string(decryptedFields["latitude"]), Valid: customer.Latitude.Valid}
		newCustomer.Longitude = sql.NullString{String: string(decryptedFields["longitude"]), Valid: customer.Longitude.Valid}

		return &newCustomer, nil
	}, nil

}

func (s *CustomerCryptoService) EncryptCustomerCreateParams(ctx context.Context, customer *db.CreateCustomerParams) (*db.CreateCustomerParams, error) {

	if !s.encryptionEnabled {
		return customer, nil
	}

	// get the latest key
	latestKey, err := s.queries.GetLatestEncryptionKey(ctx)
	if err != nil {
		return nil, fmt.Errorf("failed to get latest encryption key: %w", err)
	}

	nonBase64Key, err := base64.StdEncoding.DecodeString(latestKey.Key)
	if err != nil {
		return nil, fmt.Errorf("failed to decode key: %w", err)
	}

	// encrypt the fields
	encryptor, err := aesgcm.GetEncryptFunc(ctx, int32(latestKey.ID), nonBase64Key)
	if err != nil {
		return nil, fmt.Errorf("failed to get encrypt func: %w", err)
	}

	encryptedFields, err := aesgcm.EncryptFields(encryptor, map[string]aesgcm.PlainField{
		"first_name": aesgcm.PlainField(customer.FirstName.String),
		"last_name":  aesgcm.PlainField(customer.LastName.String),
		"phone":      aesgcm.PlainField(customer.Phone.String),
	})
	if err != nil {
		return nil, fmt.Errorf("failed to encrypt customer: %w", err)
	}

	encryptedCustomer := *customer
	encryptedCustomer.FirstName = sql.NullString{String: string(encryptedFields["first_name"].EncryptedVal), Valid: customer.FirstName.Valid}
	encryptedCustomer.LastName = sql.NullString{String: string(encryptedFields["last_name"].EncryptedVal), Valid: customer.LastName.Valid}
	encryptedCustomer.Phone = sql.NullString{String: string(encryptedFields["phone"].EncryptedVal), Valid: customer.Phone.Valid}
	encryptedCustomer.KeyIDFirstName = encryptedFields["first_name"].KeyId
	encryptedCustomer.KeyIDLastName = encryptedFields["last_name"].KeyId
	encryptedCustomer.KeyIDPhone = encryptedFields["phone"].KeyId

	return &encryptedCustomer, nil
}

func (s *CustomerCryptoService) EncryptCustomerUpdateParams(ctx context.Context, customer *db.UpdateCustomerParams) (*db.UpdateCustomerParams, error) {

	if !s.encryptionEnabled {
		return customer, nil
	}

	// get the latest key
	latestKey, err := s.queries.GetLatestEncryptionKey(ctx)
	if err != nil {
		return nil, fmt.Errorf("failed to get latest encryption key: %w", err)
	}

	nonBase64Key, err := base64.StdEncoding.DecodeString(latestKey.Key)
	if err != nil {
		return nil, fmt.Errorf("failed to decode key: %w", err)
	}

	// encrypt the fields
	encryptor, err := aesgcm.GetEncryptFunc(ctx, int32(latestKey.ID), nonBase64Key)
	if err != nil {
		return nil, fmt.Errorf("failed to get encrypt func: %w", err)
	}

	encryptedFields, err := aesgcm.EncryptFields(encryptor, map[string]aesgcm.PlainField{
		"first_name": aesgcm.PlainField(customer.FirstName.String),
		"last_name":  aesgcm.PlainField(customer.LastName.String),
		"phone":      aesgcm.PlainField(customer.Phone.String),
	})
	if err != nil {
		return nil, fmt.Errorf("failed to encrypt customer: %w", err)
	}

	encryptedCustomer := *customer
	encryptedCustomer.FirstName = sql.NullString{String: string(encryptedFields["first_name"].EncryptedVal), Valid: customer.FirstName.Valid}
	encryptedCustomer.LastName = sql.NullString{String: string(encryptedFields["last_name"].EncryptedVal), Valid: customer.LastName.Valid}
	encryptedCustomer.Phone = sql.NullString{String: string(encryptedFields["phone"].EncryptedVal), Valid: customer.Phone.Valid}
	encryptedCustomer.KeyIDFirstName = encryptedFields["first_name"].KeyId
	encryptedCustomer.KeyIDLastName = encryptedFields["last_name"].KeyId
	encryptedCustomer.KeyIDPhone = encryptedFields["phone"].KeyId

	return &encryptedCustomer, nil
}
