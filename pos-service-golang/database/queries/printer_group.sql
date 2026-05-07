-- name: GetPrinterGroupsByShopID :many
SELECT * FROM printer_group WHERE shop_id = ? AND brand_id = ?;
