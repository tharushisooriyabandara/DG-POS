package crypt

import (
	"context"
	"database/sql"

	aesgcm "github.com/Delivergate-Dev/pos-service-golang/cryptography/aes-gcm"
	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
)

type UserCryptoService struct {
	queries           *db.Queries
	encryptionEnabled bool
}

func NewUserCryptoService(conn *sql.DB, encryptionEnabled bool) *UserCryptoService {
	return &UserCryptoService{queries: db.New(conn), encryptionEnabled: encryptionEnabled}
}

func (s *UserCryptoService) DecryptUserByIDRow(ctx context.Context, user *db.GetUserByIDRow) (*db.GetUserByIDRow, error) {
	if user == nil {
		return nil, nil
	}

	// get the encrypted DEK from the database
	encryptedKeys, err := getKeyMap(ctx, s.queries)
	if err != nil {
		return nil, err
	}

	// decrypt the fields
	decryptedFields, err := aesgcm.DecryptFields(ctx, map[string]aesgcm.EncryptedField{
		"name":       toEncryptedField(user.KeyIDName, encryptedKeys[uint64(user.KeyIDName)], user.Name),
		"last_name":  toEncryptedField(user.KeyIDLastName, encryptedKeys[uint64(user.KeyIDLastName)], user.LastName.String),
		"email":      toEncryptedField(user.KeyIDEmail, encryptedKeys[uint64(user.KeyIDEmail)], user.Email),
		"address":    toEncryptedField(user.KeyIDAddress, encryptedKeys[uint64(user.KeyIDAddress)], user.Address.String),
		"contact_no": toEncryptedField(user.KeyIDContactNo, encryptedKeys[uint64(user.KeyIDContactNo)], user.ContactNo.String),
	})
	if err != nil {
		return nil, err
	}

	// set the decrypted fields
	newUser := *user
	newUser.Name = string(decryptedFields["name"])
	newUser.LastName = sql.NullString{String: string(decryptedFields["last_name"]), Valid: true}
	newUser.Email = string(decryptedFields["email"])
	newUser.Address = sql.NullString{String: string(decryptedFields["address"]), Valid: true}
	newUser.ContactNo = sql.NullString{String: string(decryptedFields["contact_no"]), Valid: true}

	return &newUser, nil
}

func (s *UserCryptoService) DecryptUsersRow(ctx context.Context, users []*db.GetUsersRow) ([]*db.GetUsersRow, error) {

	encryptedKeys, err := getKeyMap(ctx, s.queries)
	if err != nil {
		return nil, err
	}

	decryptedUsers := make([]*db.GetUsersRow, len(users))
	for i, user := range users {
		decryptedUsers[i], err = s.decryptUserRow(ctx, encryptedKeys, user)
		if err != nil {
			return nil, err
		}
	}
	return decryptedUsers, nil
}

func (s *UserCryptoService) decryptUserRow(ctx context.Context, encryptedKeys map[uint64][]byte, user *db.GetUsersRow) (*db.GetUsersRow, error) {
	if user == nil {
		return nil, nil
	}

	// decrypt the fields
	decryptedFields, err := aesgcm.DecryptFields(ctx, map[string]aesgcm.EncryptedField{
		"name":       toEncryptedField(user.KeyIDName, encryptedKeys[uint64(user.KeyIDName)], user.Name),
		"last_name":  toEncryptedField(user.KeyIDLastName, encryptedKeys[uint64(user.KeyIDLastName)], user.LastName.String),
		"email":      toEncryptedField(user.KeyIDEmail, encryptedKeys[uint64(user.KeyIDEmail)], user.Email),
		"contact_no": toEncryptedField(user.KeyIDContactNo, encryptedKeys[uint64(user.KeyIDContactNo)], user.ContactNo.String),
		"address":    toEncryptedField(user.KeyIDAddress, encryptedKeys[uint64(user.KeyIDAddress)], user.Address.String),
	})
	if err != nil {
		return nil, err
	}

	// set the decrypted fields
	newUser := *user
	newUser.Name = string(decryptedFields["name"])
	newUser.LastName = sql.NullString{String: string(decryptedFields["last_name"]), Valid: true}
	newUser.Email = string(decryptedFields["email"])
	newUser.ContactNo = sql.NullString{String: string(decryptedFields["contact_no"]), Valid: true}
	newUser.Address = sql.NullString{String: string(decryptedFields["address"]), Valid: true}

	return &newUser, nil
}
