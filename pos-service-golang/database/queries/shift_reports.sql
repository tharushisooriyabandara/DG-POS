-- name: GetShiftInfoByShop :many
WITH
    orders_and_net_totals AS (
        SELECT
            odr.id,
            odr.user_id,
            0 + SUM(
                CASE
                    WHEN ot.transaction_mode = 'CASH' THEN ot.transaction_amount * CASE
                        WHEN ot.transaction_type = 'SALE' THEN 1
                        WHEN ot.transaction_type = 'REFUND' THEN -1
                        ELSE 0
                    END
                    ELSE 0
                END
            ) AS cash_sales_total,
            0 + SUM(
                CASE
                    WHEN ot.transaction_mode = 'CARD' THEN ot.transaction_amount * CASE
                        WHEN ot.transaction_type = 'SALE' THEN 1
                        WHEN ot.transaction_type = 'REFUND' THEN -1
                        ELSE 0
                    END
                    ELSE 0
                END
            ) AS card_sales_total,
            0 + SUM(
                ot.transaction_amount * CASE
                    WHEN ot.transaction_type = 'SALE' THEN 1
                    WHEN ot.transaction_type = 'REFUND' THEN -1
                    ELSE 0
                END
            ) AS net_sales
        FROM
            `order` odr
            JOIN order_transaction ot ON odr.id = ot.type_id
        WHERE
            odr.status IN ('COMPLETED', 'CANCELED')
            AND odr.device_platform = 'dg_pos'
            AND odr.created_at >= sqlc.arg (from_created_at)
            AND odr.created_at <= sqlc.arg (to_created_at)
            AND odr.shop_id = sqlc.arg (shop_id)
        GROUP BY
            odr.id
    )
SELECT
    user_id,
    COUNT(id) AS orders_created,
    0 + SUM(cash_sales_total) AS total_cash_sales,
    0 + SUM(card_sales_total) AS total_card_sales,
    0 + SUM(net_sales) AS total_net_sales
FROM orders_and_net_totals
GROUP BY
    user_id;

-- name: GetShiftInfoByUser :many
WITH
    orders_and_net_totals AS (
        SELECT
            odr.id,
            odr.user_id,
            odr.created_at,
            0 + SUM(
                CASE
                    WHEN ot.transaction_mode = 'CASH' THEN ot.transaction_amount * CASE
                        WHEN ot.transaction_type = 'SALE' THEN 1
                        WHEN ot.transaction_type = 'REFUND' THEN -1
                        ELSE 0
                    END
                    ELSE 0
                END
            ) AS cash_sales_total,
            0 + SUM(
                CASE
                    WHEN ot.transaction_mode = 'CARD' THEN ot.transaction_amount * CASE
                        WHEN ot.transaction_type = 'SALE' THEN 1
                        WHEN ot.transaction_type = 'REFUND' THEN -1
                        ELSE 0
                    END
                    ELSE 0
                END
            ) AS card_sales_total,
            0 + SUM(
                ot.transaction_amount * CASE
                    WHEN ot.transaction_type = 'SALE' THEN 1
                    WHEN ot.transaction_type = 'REFUND' THEN -1
                    ELSE 0
                END
            ) AS net_sales
        FROM
            `order` odr
            JOIN order_transaction ot ON odr.id = ot.type_id
        WHERE
            odr.status IN ('COMPLETED', 'CANCELED')
            AND odr.device_platform = 'dg_pos'
            AND odr.created_at >= sqlc.arg (from_created_at)
            AND odr.created_at <= sqlc.arg (to_created_at)
            AND odr.shop_id = sqlc.arg (shop_id)
            AND odr.user_id = sqlc.arg (user_id)
        GROUP BY
            odr.id
    )
SELECT
    shifts.id AS shift_id,
    shifts.login,
    shifts.logout,
    COUNT(odr.id) AS orders_created,
    0 + SUM(odr.cash_sales_total) AS cash_total,
    0 + SUM(odr.card_sales_total) AS card_total,
    0 + SUM(odr.net_sales) AS total
FROM
    orders_and_net_totals odr
    JOIN dg_pos_user_shifts shifts ON shifts.user_id = odr.user_id
    AND odr.created_at BETWEEN shifts.login AND COALESCE(shifts.logout, NOW())
GROUP BY
    shifts.id;