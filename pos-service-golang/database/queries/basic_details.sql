-- name: GetShopLogo :one
SELECT brand_logo FROM basic_details WHERE webshop_brand_id = ?;