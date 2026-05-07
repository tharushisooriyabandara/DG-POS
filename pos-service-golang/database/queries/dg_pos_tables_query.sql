-- name: GetTableByID :one
SELECT * FROM dg_pos_tables WHERE id = ? LIMIT 1;

-- name: GetTableByTableOrderingsID :one
SELECT * FROM dg_pos_tables WHERE table_orderings_id = ? LIMIT 1;

-- name: GetDgPosTableName :one
SELECT name FROM dg_pos_tables WHERE id = ?;

-- name: GetDgPosTableNameByTableOrderingsID :one
SELECT name FROM dg_pos_tables WHERE table_orderings_id = ?;

-- name: UpdateTableStatus :exec
UPDATE dg_pos_tables
SET
    status = ?
WHERE
    id = ?;

-- name: UpdateTableStatusByTableOrderingsID :exec
UPDATE dg_pos_tables
SET
    status = ?
WHERE
    table_orderings_id = ?;

-- name: AddOngoingOrderForTable :exec
INSERT INTO
    dg_pos_tables_ongoing_orders (table_id, order_id)
VALUES (?, ?);

-- name: RemoveOngoingOrderForTable :exec
DELETE FROM dg_pos_tables_ongoing_orders WHERE table_id = ?;

-- name: GetTables :many
SELECT tbs.*
FROM
    dg_pos_tables tbs
WHERE (
        sqlc.narg (outlet_id) IS NULL
        OR tbs.shop_id = sqlc.narg (outlet_id)
    )
    AND (
        sqlc.narg (brand_id) IS NULL
        OR tbs.brand_id = sqlc.narg (brand_id)
    );

-- name: GetOngoingOrdersForTable :many
SELECT 
    too.*,
    o.display_order_id,
    o.status,
    o.total_amount,
    o.customer_name,
    o.order_session_id,
    o.is_table_order,
    o.shipping_method,
    o.created_at,
    CASE WHEN o.payment_mode IS NULL THEN 'UNPAID' ELSE 'PAID' END AS payment_status,
    p.logo AS platform_logo,
    tom.name AS table_order_method_name
FROM dg_pos_tables_ongoing_orders too 
JOIN `order` o ON too.order_id = o.id
JOIN delivery_platform dp ON o.platform_id = dp.id
JOIN platform p ON dp.platform_id = p.id
LEFT JOIN table_order_methods tom ON tom.id = o.table_order_method_id
WHERE too.table_id = ?;