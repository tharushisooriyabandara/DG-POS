-- name: CreateOrderRefund :execlastid
INSERT INTO
    order_refund (
        type_id,
        `type`,
        transaction_id,
        refund_amount,
        refund_mode,
        reason,
        created_at,
        updated_at
    )
VALUES (?, ?, ?, ?, ?, ?, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);

-- name: GetLatestRefundByOrderID :one
SELECT 
  sqlc.embed(ot),
  orf.id AS refund_id,
  orf.reason
FROM order_refund orf
JOIN order_transaction ot ON ot.id = orf.transaction_id
WHERE 
  orf.type_id = ?
  AND orf.`type` = 'ORDER'
ORDER BY id DESC
LIMIT 1;

-- name: GetRefundByID :one
SELECT * FROM order_refund WHERE id = ?;

-- name: GetRefundsByOrderID :many
SELECT * FROM order_refund WHERE type_id = ? AND `type` = 'ORDER';
