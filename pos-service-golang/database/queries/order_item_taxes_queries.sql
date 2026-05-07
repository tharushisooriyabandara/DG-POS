-- name: CreateOrderItemTax :exec
INSERT INTO
    order_item_tax (
        order_id,
        order_item_id,
        order_item_modifier_id,
        item_price,
        tax_id,
        tax_profile_id,
        tax_rule_id,
        amount,
        created_at,
        updated_at
    )
VALUES (?, ?, ?, ?, ?, ?, ?, ?, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP); 

-- name: DeleteOrderItemTaxes :exec
DELETE FROM order_item_tax WHERE order_id = ?;

-- name: GetOrderItemTaxes :many
SELECT 
  oit.*,
  t.code AS tax_code,
  t.rate AS tax_rate
FROM 
  order_item_tax oit 
  JOIN taxes_main t ON t.id = oit.tax_id
WHERE 
  oit.order_id = ?;
