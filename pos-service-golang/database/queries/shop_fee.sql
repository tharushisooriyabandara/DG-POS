-- name: GetShopFees :many
SELECT 
  sf.*,
  t.*
FROM shop_fee sf
LEFT JOIN taxes_main t ON t.id = sf.tax_id
WHERE
    sf.shop_id = ?
    AND sf.webshop_brand_id = ?
    AND sf.status = 1
    AND sf.platform = 'DG_POS';

-- name: GetShopFeesByOrderID :many
SELECT 
  osf.*, 
  t.code AS tax_code,
  t.rate AS tax_rate,
  sf.fee_name, 
  sf.fee_type
FROM
  order_shop_fee osf
  JOIN shop_fee sf ON sf.id = osf.shop_fee_id
  LEFT JOIN taxes_main t ON t.id = osf.tax_id
WHERE
  osf.order_id = ?;

-- name: DeleteOrderShopFees :exec
DELETE FROM order_shop_fee WHERE order_id = ?;
