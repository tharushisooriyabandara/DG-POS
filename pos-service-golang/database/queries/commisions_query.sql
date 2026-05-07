-- name: GetCommissionAmount :one
SELECT order_commission
FROM shop_brand_details
WHERE
    shop_id = ?
    AND webshop_brand_id = ?;

-- name: CreateOrderCommission :exec
INSERT INTO
    order_commission (
        order_tmp_id,
        order_id,
        commission,
        reference,
        report_id,
        status,
        created_at,
        updated_at
    )
VALUES (?, ?, ?, ?, ?, ?, ?, ?);

-- name: UpdateOrderCommission :exec
UPDATE order_commission
SET
    commission = ?,
    updated_at = ?
WHERE
    order_id = ?;

-- name: DeleteOrderCommission :exec
DELETE FROM order_commission WHERE order_id = ?;

-- name: GetOrderCommission :one
SELECT * FROM order_commission WHERE order_id = ?;