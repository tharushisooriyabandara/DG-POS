-- name: CreateOrderTransaction :execlastid
INSERT INTO
    order_transaction (
        type_id,
        `type`,
        transaction_type,
        transaction_mode,
        transaction_amount,
        payment_type,
        platform,
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
        "DG_POS",
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP
    );

-- name: GetOrderTransactions :many
SELECT
    sqlc.embed(ot),
    orf.id AS refund_id,
    orf.reason,
    orf.transaction_id AS refund_transaction_id
FROM
    order_transaction ot
    LEFT JOIN order_refund orf ON orf.transaction_id = ot.id
WHERE
    ot.type_id = sqlc.arg (type_id)
ORDER BY ot.id DESC;

-- name: GetCashTransactionsByOrderID :many
SELECT *
FROM order_transaction
WHERE
    type_id = ?
    AND transaction_mode = 'CASH';

-- name: GetOrderTransactionByRefundID :one
SELECT sqlc.embed(ot), orf.reason, orf.transaction_id AS refund_transaction_id
FROM
    order_transaction ot
    LEFT JOIN order_refund orf ON orf.transaction_id = ot.id
WHERE
    orf.id = ?;

-- name: GetSaleTransactionByOrderID :one
SELECT *
FROM order_transaction
WHERE
    type_id = ?
    AND transaction_type = 'SALE'
    AND `type` = 'ORDER';

-- name: GetTransactionByID :one
SELECT * FROM order_transaction WHERE id = ?;

-- name: GetNetCashAndCardSaleByOrderID :one
SELECT 
--   0 + SUM(
--         CASE
--             WHEN ot.transaction_mode = 'CASH' THEN ot.transaction_amount * CASE
--                 WHEN ot.transaction_type = 'SALE' THEN 1
--                 WHEN ot.transaction_type = 'REFUND' THEN -1
--                 ELSE 0
--             END
--             ELSE 0
--         END
--     ) AS net_cash_sales,
--     0 + SUM(
--         CASE
--             WHEN ot.transaction_mode = 'CARD' THEN ot.transaction_amount * CASE
--                 WHEN ot.transaction_type = 'SALE' THEN 1
--                 WHEN ot.transaction_type = 'REFUND' THEN -1
--                 ELSE 0
--             END
--             ELSE 0
--         END
--     ) AS net_card_sales,
    0 + SUM(
        CASE
            WHEN ot.transaction_type = 'SALE' THEN ot.transaction_amount
            WHEN ot.transaction_type = 'REFUND' THEN -ot.transaction_amount
            ELSE 0
        END
    ) AS net_sales
FROM
    order_transaction ot
WHERE
    ot.type_id = ?
    AND ot.`type` = 'ORDER'
GROUP BY
    ot.type_id;

-- name: GetSalesAndRefundByTypeID :one
SELECT
    0 + SUM(
        CASE
            WHEN ot.transaction_mode = 'CASH' THEN ot.transaction_amount * CASE
                WHEN ot.transaction_type = 'SALE' THEN 1
                ELSE 0
            END
            ELSE 0
        END
    ) AS cash_sale,
    0 + SUM(
        CASE
            WHEN ot.transaction_mode = 'CARD' THEN ot.transaction_amount * CASE
                WHEN ot.transaction_type = 'SALE' THEN 1
                ELSE 0
            END
            ELSE 0
        END
    ) AS card_sale,
    0 + SUM(
        CASE
            WHEN ot.transaction_mode = 'CASH' THEN ot.transaction_amount * CASE
                WHEN ot.transaction_type = 'REFUND' THEN 1
                ELSE 0
            END
            ELSE 0
        END
    ) AS cash_refund,
    0 + SUM(
        CASE
            WHEN ot.transaction_mode = 'CARD' THEN ot.transaction_amount * CASE
                WHEN ot.transaction_type = 'REFUND' THEN 1
                ELSE 0
            END
            ELSE 0
        END
    ) AS card_refund,
    0 + SUM(
        CASE
            WHEN ot.transaction_type = 'SALE' THEN ot.transaction_amount
            WHEN ot.transaction_type = 'REFUND' THEN -ot.transaction_amount
            ELSE 0
        END
    ) AS net_sale
FROM order_transaction ot
WHERE
    ot.type_id = ?
GROUP BY
    ot.type_id;
