-- name: GetEncryptionKeys :many
SELECT * FROM encryption_keys;

-- name: GetLatestEncryptionKey :one
SELECT * FROM encryption_keys ORDER BY id DESC LIMIT 1;