-- name: CreateOrderItem :execlastid
INSERT INTO
    order_items (
        order_id,
        item_id,
        quantity,
        price_per_item,
        total,
        original_price,
        is_sale,
        discount_amount,
        status,
        created_at,
        updated_at,
        item_name,
        category_name,
        modifiers,
        tax,
        note
    )
VALUES (
        ?,
        ?,
        ?,
        ?,
        ?,
        ?,
        ?,
        ?,
        ?,
        ?,
        ?,
        ?,
        ?,
        ?,
        ?,
        ?
    );

-- name: GetOrderItemsByOrderID :many
select 
  oi.*,
  oit.id as order_item_tax_id,
  oit.tax_profile_id as tax_profile_id,
  oit.tax_rule_id as tax_rule_id,
  oit.tax_id as tax_id,
  oit.amount as tax_amount,
  t.code as tax_code,
  t.rate as tax_rate
from order_items oi
left join order_item_tax oit on oit.order_item_id = oi.id and oit.order_item_modifier_id is null
left join taxes_main t on t.id = oit.tax_id
where oi.order_id = ?;

-- name: GetAllOrderItemIDs :many
SELECT id FROM order_items WHERE order_id = ?;

-- name: DeleteOrderItems :exec
DELETE FROM order_items WHERE order_id = ?;

-- name: GetAvailableOrderItems :many
SELECT *
FROM entity_delivery_platform
WHERE
    entity_id IN (sqlc.slice (item_ids))
    AND delivery_platform_id = ?
    AND available = 1;

-- name: UpdateOrderItemsStatus :exec
UPDATE `order_items` SET status = ? WHERE order_id = ?;

