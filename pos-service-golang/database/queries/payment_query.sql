-- Active: 1748425317602@@127.0.0.1@3306@subway
-- name: CreateDgPosPayments :exec
INSERT INTO
    `dg_pos_payments` (
        order_id,
        date_time,
        amount,
        cash,
        balance,
        card_transaction_token,
        transaction_id,
        refund_id,
        payment_method_id,
        payment_type,
        status,
        is_refund,
        created_at,
        updated_at
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
        ?
    );

-- name: UpdateDgPosPayment :exec
UPDATE `dg_pos_payments`
SET
    amount = ?,
    cash = ?,
    balance = ?,
    transaction_id = ?,
    card_transaction_token = ?,
    payment_method_id = ?,
    status = ?,
    updated_at = ?
WHERE
    order_id = ?;

-- name: DeleteDgPosPayments :exec
DELETE FROM `dg_pos_payments` WHERE order_id = ?;

-- name: DeletePayments :exec
DELETE FROM `payments` WHERE order_id = ?;

-- name: CreatePayment :exec
INSERT INTO
    payments (
        order_id,
        date_time,
        amount,
        tax,
        discount,
        transaction_id,
        payment_method_id,
        status,
        created_at,
        updated_at
    )
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);

-- name: UpdatePayment :exec
UPDATE `payments`
SET
    amount = ?,
    tax = ?,
    discount = ?,
    payment_method_id = ?,
    updated_at = ?
WHERE
    order_id = ?;

-- name: GetPaymentDetails :many
SELECT * FROM dg_pos_payments WHERE order_id = ?;

-- name: GetDgPaymentStatusByOrderId :one
SELECT status, payment_type FROM dg_pos_payments WHERE order_id = ?;

-- name: GetWebshopPaymentStatusByOrderId :one
SELECT payment_status, payment_type FROM webshop_payments WHERE order_id = ?;
