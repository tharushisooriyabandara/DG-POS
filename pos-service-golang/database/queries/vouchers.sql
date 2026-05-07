-- name: UpdateVoucherUsage :exec
UPDATE vouchers
SET
    current_usage = current_usage + sqlc.arg (increment_by)
WHERE
    voucher_code = ?;

-- name: GetVoucherIdAndType :one
SELECT id, voucher_type FROM vouchers WHERE voucher_code = ?;

-- name: CreateCustomerVoucher :exec
INSERT INTO
    customer_vouchers (
        customer_id,
        voucher_id,
        is_used,
        created_at,
        updated_at
    )
VALUES (?, ?, ?, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);

-- name: UpdateCustomerVoucher :exec
UPDATE customer_vouchers
SET
    is_used = ?,
    updated_at = CURRENT_TIMESTAMP
WHERE
    customer_id = ?
    AND voucher_id = ?;

-- name: DeleteCustomerVoucher :exec
DELETE FROM customer_vouchers
WHERE
    customer_id = ?
    AND voucher_id = ?;