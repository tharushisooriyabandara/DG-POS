-- name: GetShopByID :one
SELECT * FROM shop WHERE id = ?;

-- name: GetShopByCode :one
SELECT * FROM shop WHERE code = ?;