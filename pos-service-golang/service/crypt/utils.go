package crypt

import (
	"context"
	"crypto/sha256"
	"encoding/base64"
	"encoding/hex"

	aesgcm "github.com/Delivergate-Dev/pos-service-golang/cryptography/aes-gcm"
	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
)

func getKeyMap(ctx context.Context, queries *db.Queries) (map[uint64][]byte, error) {
	encryptedKeys, err := queries.GetEncryptionKeys(ctx)
	if err != nil {
		return nil, err
	}

	keyMap := make(map[uint64][]byte)
	for _, key := range encryptedKeys {
		encryptedKey, err := base64.StdEncoding.DecodeString(key.Key)
		if err != nil {
			return nil, err
		}
		keyMap[key.ID] = encryptedKey
	}
	return keyMap, nil
}

func ToHashed(val string) string {
	hash := sha256.Sum256([]byte(val))
	return hex.EncodeToString(hash[:])
}

func toEncryptedField(keyId int32, encryptedKey []byte, val string) aesgcm.EncryptedField {
	return aesgcm.EncryptedField{
		KeyId:        keyId,
		EncryptedDEK: encryptedKey,
		EncryptedVal: []byte(val),
	}
}
