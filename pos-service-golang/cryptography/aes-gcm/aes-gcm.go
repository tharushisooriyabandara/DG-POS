package aesgcm

import (
	"context"
	"crypto/aes"
	"crypto/cipher"
	"crypto/rand"
	"encoding/base64"
	"errors"
	"fmt"
	"io"

	awskms "github.com/Delivergate-Dev/pos-service-golang/cryptography/aws-kms"
)

type PlainField = []byte

type keyId = int32
type EncryptedField struct {
	KeyId        keyId
	EncryptedDEK []byte
	EncryptedVal []byte
}

type EncryptFunc func(PlainField) (EncryptedField, error)
type DecryptFunc func(EncryptedField) (PlainField, error)

func GetEncryptFunc(ctx context.Context, keyId keyId, encryptedKey []byte) (EncryptFunc, error) {
	gcm, err := getGCM(ctx, keyId, encryptedKey)
	if err != nil {
		return nil, fmt.Errorf("failed to get gcm: %w", err)
	}

	return func(plainField PlainField) (EncryptedField, error) {
		nonce := make([]byte, gcm.NonceSize())
		if _, err := io.ReadFull(rand.Reader, nonce); err != nil {
			return EncryptedField{}, fmt.Errorf("failed to create nonce: %w", err)
		}

		// Seal appends ciphertext||tag to the dst (which we start empty).
		ciphertext := gcm.Seal(nil, nonce, plainField, nil)

		// Store nonce + ciphertext+tag. Persist both.
		out := make([]byte, 0, len(nonce)+len(ciphertext))
		out = append(out, nonce...)
		out = append(out, ciphertext...)

		out = []byte(base64.StdEncoding.EncodeToString(out))

		// Encode the output to base64 before returning
		return EncryptedField{KeyId: keyId, EncryptedVal: out}, nil
	}, nil
}

func GetDecryptFunc(ctx context.Context, keyId keyId, encryptedKey []byte) (DecryptFunc, error) {
	gcm, err := getGCM(ctx, keyId, encryptedKey)
	if err != nil {
		return nil, fmt.Errorf("failed to get gcm: %w", err)
	}

	return func(encryptedField EncryptedField) (PlainField, error) {
		decodedBlob, err := base64.StdEncoding.DecodeString(string(encryptedField.EncryptedVal))
		if err != nil {
			return nil, fmt.Errorf("failed to decode base64: %w", err)
		}

		ns := gcm.NonceSize()
		if len(decodedBlob) < ns+gcm.Overhead() {
			return nil, errors.New("ciphertext too short")
		}
		nonce := decodedBlob[:ns]
		ct := decodedBlob[ns:]

		// Open verifies integrity; if tampered, it returns an error.
		plain, err := gcm.Open(nil, nonce, ct, nil)
		if err != nil {
			return nil, fmt.Errorf("failed to open: %w", err)
		}
		return plain, nil
	}, nil
}

func EncryptFields(encryptFunc EncryptFunc, fields map[string]PlainField) (map[string]EncryptedField, error) {
	encryptedFields := make(map[string]EncryptedField)

	for name, field := range fields {

		if string(field) == "" {
			encryptedFields[name] = EncryptedField{KeyId: 0, EncryptedVal: field}
			continue
		}

		encrypted, err := encryptFunc(field)
		if err != nil {
			return nil, fmt.Errorf("failed to encrypt %s: %w", name, err)
		}
		encryptedFields[name] = encrypted
	}

	return encryptedFields, nil
}

func DecryptFields(ctx context.Context, fields map[string]EncryptedField) (map[string]PlainField, error) {
	// cache of funcs per keyID
	decryptors := make(map[keyId]DecryptFunc)

	decryptedFields := make(map[string]PlainField)
	for name, field := range fields {
		if field.KeyId == 0 {
			decryptedFields[name] = PlainField(field.EncryptedVal)
			continue
		}
		if _, ok := decryptors[field.KeyId]; !ok {
			var err error
			decryptors[field.KeyId], err = GetDecryptFunc(ctx, field.KeyId, field.EncryptedDEK)
			if err != nil {
				return nil, fmt.Errorf("failed to get decrypt func for %s: %w", name, err)
			}
		}
		decrypted, err := decryptors[field.KeyId](field)
		if err != nil {
			return nil, fmt.Errorf("failed to decrypt %s: %w", name, err)
		}
		decryptedFields[name] = decrypted
	}

	return decryptedFields, nil
}

func getGCM(ctx context.Context, keyId keyId, encryptedKey []byte) (cipher.AEAD, error) {
	key, err := awskms.GetPlainTextKey(ctx, keyId, encryptedKey)
	if err != nil {
		return nil, fmt.Errorf("failed to get data key: %w", err)
	}

	block, err := aes.NewCipher(key)
	if err != nil {
		return nil, fmt.Errorf("failed to create cipher: %w", err)
	}
	gcm, err := cipher.NewGCM(block)
	if err != nil {
		return nil, fmt.Errorf("failed to create gcm: %w", err)
	}
	return gcm, nil
}

func encryptGCM(ctx context.Context, keyId keyId, encryptedKey, plaintext []byte) ([]byte, error) {

	gcm, err := getGCM(ctx, keyId, encryptedKey)
	if err != nil {
		return nil, err
	}

	nonce := make([]byte, gcm.NonceSize())
	if _, err := io.ReadFull(rand.Reader, nonce); err != nil {
		return nil, err
	}

	// Seal appends ciphertext||tag to the dst (which we start empty).
	ciphertext := gcm.Seal(nil, nonce, plaintext, nil)

	// Store nonce + ciphertext+tag. Persist both.
	out := make([]byte, 0, len(nonce)+len(ciphertext))
	out = append(out, nonce...)
	out = append(out, ciphertext...)

	// Encode the output to base64 before returning
	return []byte(base64.StdEncoding.EncodeToString(out)), nil

}

func decryptGCM(ctx context.Context, keyId keyId, encryptedKey, blob []byte) ([]byte, error) {

	gcm, err := getGCM(ctx, keyId, encryptedKey)
	if err != nil {
		return nil, err
	}

	decodedBlob, err := base64.StdEncoding.DecodeString(string(blob))
	if err != nil {
		return nil, fmt.Errorf("failed to decode base64: %w", err)
	}

	ns := gcm.NonceSize()
	if len(decodedBlob) < ns+gcm.Overhead() {
		return nil, errors.New("ciphertext too short")
	}
	nonce := decodedBlob[:ns]
	ct := decodedBlob[ns:]

	// Open verifies integrity; if tampered, it returns an error.
	plain, err := gcm.Open(nil, nonce, ct, nil)
	if err != nil {
		return nil, fmt.Errorf("failed to open: %w", err)
	}
	return plain, nil
}
