-- name: CreateOrderStatusTimestamps :exec
INSERT INTO
    dg_pos_order_status_timestamps (
        order_id,
        queue,
        preparing,
        ready,
        served,
        delivered,
        completed,
        created_at,
        updated_at
    )
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?);

-- name: UpdateOrderTimestamps :exec
UPDATE dg_pos_order_status_timestamps
SET
    queue = ?,
    preparing = ?,
    ready = ?,
    served = ?,
    delivered = ?,
    completed = ?,
    updated_at = ?
WHERE
    order_id = ?;

-- name: GetOrderStatusTimestamps :one
SELECT * FROM dg_pos_order_status_timestamps WHERE order_id = ?;