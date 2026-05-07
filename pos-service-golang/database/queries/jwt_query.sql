-- name: CreateRefreshToken :exec
INSERT INTO pos_refresh_tokens (user_id, token, expires_at) VALUES (?, ?, ?);

-- name: DeleteRefreshToken :exec
DELETE FROM pos_refresh_tokens WHERE user_id = ?;

-- name: GetRefreshToken :one
SELECT * FROM pos_refresh_tokens WHERE user_id = ?;