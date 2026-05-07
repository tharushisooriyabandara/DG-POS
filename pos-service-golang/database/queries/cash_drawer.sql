-- name: CreateCashDrawer :exec
INSERT INTO
    dg_pos_cash_drawer (
        outlet_id,
        is_active,
        created_at,
        updated_at
    )
VALUES (
        ?,
        ?,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP
    );

-- name: GetCashDrawerByOutletID :one
SELECT * FROM dg_pos_cash_drawer WHERE is_active AND outlet_id = ?;

-- name: CreateCashDrawerSession :exec
INSERT INTO
    dg_pos_cash_drawer_sessions (
        cash_drawer_id,
        session_started_user_id,
        opened_at,
        opening_balance,
        created_at,
        updated_at
    )
VALUES (
        ?,
        ?,
        ?,
        ?,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP
    );

-- name: GetOpenCashDrawerSession :one
SELECT
    cds.*,
    u_started.name AS session_started_user_name,
    u_started.key_id_name AS session_started_user_key_id_name,
    u_started.last_name AS session_started_user_last_name,
    u_started.key_id_last_name AS session_started_user_key_id_last_name
FROM
    dg_pos_cash_drawer_sessions cds
    JOIN dg_pos_cash_drawer cd ON cd.id = cds.cash_drawer_id
    JOIN users u_started ON u_started.id = cds.session_started_user_id
WHERE
    cd.outlet_id = ?
    AND cds.status = 'OPEN'
LIMIT 1;

-- name: GetCashDrawerSessionByID :one
SELECT
    cds.*,
    u_started.name AS session_started_user_name,
    u_started.key_id_name AS session_started_user_key_id_name,
    u_started.last_name AS session_started_user_last_name,
    u_started.key_id_last_name AS session_started_user_key_id_last_name,
    u_ended.name AS session_ended_user_name,
    u_ended.key_id_name AS session_ended_user_key_id_name,
    u_ended.last_name AS session_ended_user_last_name,
    u_ended.key_id_last_name AS session_ended_user_key_id_last_name
FROM
    dg_pos_cash_drawer_sessions cds
    JOIN dg_pos_cash_drawer cd ON cd.id = cds.cash_drawer_id
    JOIN users u_started ON u_started.id = cds.session_started_user_id
    JOIN users u_ended ON u_ended.id = cds.session_ended_user_id
WHERE
    cds.id = ?;

-- name: GetCashDrawerSessions :many
SELECT
    cds.*,
    u_started.name AS session_started_user_name,
    u_started.key_id_name AS session_started_user_key_id_name,
    u_started.last_name AS session_started_user_last_name,
    u_started.key_id_last_name AS session_started_user_key_id_last_name,
    u_ended.name AS session_ended_user_name,
    u_ended.key_id_name AS session_ended_user_key_id_name,
    u_ended.last_name AS session_ended_user_last_name,
    u_ended.key_id_last_name AS session_ended_user_key_id_last_name
FROM
    dg_pos_cash_drawer_sessions cds
    JOIN dg_pos_cash_drawer cd ON cd.id = cds.cash_drawer_id
    JOIN users u_started ON u_started.id = cds.session_started_user_id
    JOIN users u_ended ON u_ended.id = cds.session_ended_user_id
WHERE
    cd.outlet_id = ?
    AND cds.status = 'CLOSED'
    AND cds.created_at >= sqlc.arg (from_date)
    AND cds.created_at <= sqlc.arg (to_date);

-- name: UpdateClosingBalanceCounted :exec
UPDATE dg_pos_cash_drawer_sessions
SET
    closing_balance_counted = ?
WHERE
    id = ?;

-- name: CloseCashDrawerSession :exec
UPDATE dg_pos_cash_drawer_sessions
SET
    closed_at = CURRENT_TIMESTAMP,
    session_ended_user_id = ?,
    closing_balance_expected = ?,
    difference = ?,
    total_in_amount = ?,
    total_out_amount = ?,
    total_sales_amount = ?,
    total_other_sales_amount = ?,
    total_refund_amount = ?,
    status = 'CLOSED',
    updated_at = CURRENT_TIMESTAMP
WHERE
    id = ?;

-- name: GetCashMovement :one
SELECT cm.*
FROM dg_pos_cash_movements cm
WHERE
    cash_drawer_session_id = ?
    AND movement_type = ?;

-- name: UpsertCashMovement :exec
INSERT INTO
    dg_pos_cash_movements (
        id,
        cash_drawer_session_id,
        movement_type,
        note,
        amount,
        user_id,
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
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP
    )
ON DUPLICATE KEY UPDATE
    amount = amount +
VALUES (amount),
    note = VALUES(note),
    updated_at = CURRENT_TIMESTAMP;

-- name: GetCashDrawerTransactionHistory :many
SELECT
    cd.id AS cash_drawer_id,
    cds.id AS cash_drawer_session_id,
    cm.id AS cash_movement_id,
    cm.created_at,
    cm.movement_type,
    cm.note,
    cm.amount,
    users.name AS user_name,
    users.key_id_name AS user_key_id_name,
    users.last_name AS user_last_name,
    users.key_id_last_name AS user_key_id_last_name
FROM
    dg_pos_cash_movements cm
    LEFT JOIN users ON users.id = cm.user_id
    JOIN dg_pos_cash_drawer_sessions cds ON cds.id = cm.cash_drawer_session_id
    JOIN dg_pos_cash_drawer cd ON cd.id = cds.cash_drawer_id
WHERE
    cd.outlet_id = sqlc.arg (outlet_id)
    AND cm.movement_type IN ('PAY_IN', 'PAY_OUT')
    AND cm.created_at >= sqlc.arg (from_date)
    AND cm.created_at <= sqlc.arg (to_date)
ORDER BY cm.id DESC;

-- name: GetCashDrawerSessionSummary :one
SELECT
    cds.id AS cash_drawer_session_id,
    0 + SUM(
        CASE
            WHEN cdm.movement_type = 'PAY_IN' THEN cdm.amount
            ELSE 0
        END
    ) AS total_in_amount,
    0 + SUM(
        CASE
            WHEN cdm.movement_type = 'PAY_OUT' THEN cdm.amount
            ELSE 0
        END
    ) AS total_out_amount,
    0 + SUM(
        CASE
            WHEN cdm.movement_type = 'SALE' THEN cdm.amount
            ELSE 0
        END
    ) + SUM(
        CASE
            WHEN cdm.movement_type = 'REFUND' THEN cdm.amount
            ELSE 0
        END
    ) AS total_sales_amount,
    0 + SUM(
        CASE
            WHEN cdm.movement_type = 'OTHER_SALES' THEN cdm.amount
            ELSE 0
        END
    ) + SUM(
        CASE
            WHEN cdm.movement_type = 'OTHER_REFUND' THEN cdm.amount
            ELSE 0
        END
    ) AS total_other_sales_amount,
    0 + SUM(
        CASE
            WHEN cdm.movement_type = 'REFUND' THEN cdm.amount
            ELSE 0
        END
    ) AS total_cash_sale_cash_refund_amount,
    0 + SUM(
        CASE
            WHEN cdm.movement_type = 'CARD_SALE_CASH_REFUND' THEN cdm.amount
            ELSE 0
        END
    ) AS total_card_sale_cash_refund_amount,
    0 + SUM(
        CASE
            WHEN cdm.movement_type = 'POS_CARD_SALE_CASH_REFUND' THEN cdm.amount
            ELSE 0
        END
    ) AS total_pos_card_sale_cash_refund_amount,
    0 + SUM(
        CASE
            WHEN cdm.movement_type = 'OTHER_REFUND' THEN cdm.amount
            ELSE 0
        END
    ) AS total_other_refund_amount,
    0 + SUM(
        CASE
            WHEN cdm.movement_type = 'REFUND'
            OR cdm.movement_type = 'CARD_SALE_CASH_REFUND'
            OR cdm.movement_type = 'POS_CARD_SALE_CASH_REFUND'
            OR cdm.movement_type = 'OTHER_REFUND' THEN cdm.amount
            ELSE 0
        END
    ) AS total_refund_amount,
    0 + COALESCE(SUM(cdm.amount), 0) AS total_movements,
    cds.opening_balance + COALESCE(SUM(cdm.amount), 0) AS expected_balance,
    cds.closing_balance_counted - (
        cds.opening_balance + COALESCE(SUM(cdm.amount), 0)
    ) AS difference
FROM
    dg_pos_cash_drawer_sessions cds
    LEFT JOIN dg_pos_cash_movements cdm ON cds.id = cdm.cash_drawer_session_id
WHERE
    cds.id = ?
GROUP BY
    cds.id;

-- name: GetCashPaidPosOrderIdsWithinTimeRange :many
SELECT
    DISTINCT odr.id
FROM
    `order` odr
    JOIN order_transaction ot ON odr.id = ot.type_id AND ot.`type` = 'ORDER'
WHERE
    -- ot.transaction_type = 'SALE'
    ot.transaction_mode = 'CASH'
    AND odr.status IN ('COMPLETED', 'CANCELED')
    AND odr.device_platform = 'dg_pos'
    AND odr.testing_order = 0
    AND odr.created_at >= sqlc.arg (from_created_at)
    AND odr.created_at <= sqlc.arg (to_created_at)
    AND odr.shop_id = ?;

-- -- name: GetSalesCashAmountPOS :one
-- SELECT 0 + COALESCE(SUM(ot.transaction_amount), 0) AS sales_total
-- FROM
--     `order` odr
--     JOIN order_transaction ot ON odr.id = ot.type_id AND ot.`type` = 'ORDER'
-- WHERE
--     ot.transaction_type = 'SALE'
--     AND ot.transaction_mode = 'CASH'
--     AND odr.status IN ('COMPLETED', 'CANCELED')
--     AND odr.device_platform = 'dg_pos'
--     AND odr.testing_order = 0
--     AND odr.created_at >= sqlc.arg (from_created_at)
--     AND odr.created_at <= sqlc.arg (to_created_at)
--     AND odr.shop_id = ?;

-- -- name: GetRefundCashAmountPOS :one
-- SELECT 0 + COALESCE(SUM(ot.transaction_amount), 0) AS refund_total
-- FROM
--     `order` odr
--     JOIN order_transaction ot ON odr.id = ot.type_id AND ot.`type` = 'ORDER'
-- WHERE
--     ot.transaction_type = 'REFUND'
--     AND ot.transaction_mode = 'CASH'
--     AND odr.status IN ('COMPLETED', 'CANCELED')
--     AND odr.device_platform = 'dg_pos'
--     AND odr.testing_order = 0
--     AND odr.created_at >= sqlc.arg (from_created_at)
--     AND odr.created_at <= sqlc.arg (to_created_at)
--     AND odr.shop_id = ?;

-- -- name: GetCashSaleCashRefundAmountPOS :one
-- WITH cash_paid_orders_within_time_range AS (
--     SELECT odr.id
--     FROM 
--         `order` odr
--         JOIN order_transaction ot ON odr.id = ot.type_id AND ot.`type` = 'ORDER'
--     WHERE
--         ot.transaction_type = 'SALE'
--         AND ot.transaction_mode = 'CASH'
--         AND odr.status IN ('COMPLETED', 'CANCELED')
--         AND odr.device_platform = 'dg_pos'
--         AND odr.testing_order = 0
--         AND odr.created_at >= sqlc.arg (from_created_at)
--         AND odr.created_at <= sqlc.arg (to_created_at)
--         AND odr.shop_id = ?
-- )
-- SELECT 0 + COALESCE(SUM(ot.transaction_amount), 0) AS refund_total
-- FROM order_transaction ot
--     JOIN cash_paid_orders_within_time_range cpo ON cpo.id = ot.type_id
-- WHERE
--     ot.transaction_type = 'REFUND'
--     AND ot.transaction_mode = 'CASH';

-- -- name: GetRefundCardAmount :one
-- WITH card_paid_orders_within_time_range AS (
--     SELECT odr.id
--     FROM 
--         `order` odr
--         JOIN order_transaction ot ON odr.id = ot.type_id AND ot.`type` = 'ORDER'
--     WHERE
--         ot.transaction_type = 'SALE'
--         AND ot.transaction_mode = 'CARD'
--         AND odr.status IN ('COMPLETED', 'CANCELED')
--         AND odr.testing_order = 0
--         AND odr.created_at >= sqlc.arg (from_created_at)
--         AND odr.created_at <= sqlc.arg (to_created_at)
--         AND odr.shop_id = ?
-- )
-- SELECT 0 + COALESCE(SUM(ot.transaction_amount), 0) AS refund_total
-- FROM order_transaction ot
--     JOIN card_paid_orders_within_time_range cpo ON cpo.id = ot.type_id
-- WHERE
--     ot.transaction_type = 'REFUND'
--     AND ot.transaction_mode = 'CASH';

-- name: GetIncompleteOrders :many
SELECT *
FROM `order` odr
WHERE
    odr.status NOT IN(
        'COMPLETED',
        'CANCELED',
        'MISSED',
        'DENIED',
        'CREATED',
        'TEMP'
    )
    AND odr.created_at BETWEEN ? AND ?
    AND odr.shop_id = ?;
