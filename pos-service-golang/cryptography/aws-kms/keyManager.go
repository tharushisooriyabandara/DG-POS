package awskms

import (
	"context"
	"sync"
	"time"
)

const cacheTTL = 10 * time.Minute

var km *keyManager

func GetPlainTextKey(ctx context.Context, keyId int32, encryptedKey []byte) (plainTextKey, error) {
	now := time.Now()

	// check if key is in cache
	km.mu.RLock()
	entry, ok := km.keyCache[keyId]
	km.mu.RUnlock()
	if ok {
		if !entry.isExpired() {
			return entry.key, nil
		} else {
			entry.zeroize()
			delete(km.keyCache, keyId)
		}
	}

	// if no, decrypt and cache
	plaintext, err := decryptDEK(ctx, encryptedKey)
	if err != nil {
		return nil, err
	}

	km.mu.Lock()
	km.keyCache[keyId] = keyEntry{key: plaintext, expiresAt: now.Add(cacheTTL)}
	km.mu.Unlock()

	return plaintext, nil
}

type plainTextKey []byte

type keyManager struct {
	mu       sync.RWMutex
	keyCache map[int32]keyEntry
}

func InitKeyManager() {
	if km == nil {
		km = &keyManager{
			keyCache: make(map[int32]keyEntry),
		}
	}
}

type keyEntry struct {
	key       plainTextKey
	expiresAt time.Time
}

func (k *keyEntry) isExpired() bool {
	return time.Now().After(k.expiresAt)
}

func (k *keyEntry) zeroize() {
	for i := range k.key {
		k.key[i] = 0
	}
}
