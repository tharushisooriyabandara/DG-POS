-- name: CreateOrder :execlastid
INSERT INTO
    `order` (
        shop_id,
        platform_id,
        remote_order_id,
        display_order_id,
        delivery_date_time,
        total_amount,
        sub_total,
        total_fee,
        campaign_code,
        discount,
        voucher_discount,
        discount_mode_applied,
        discount_percentage_applied,
        discount_type,
        vouchers,
        status,
        cancelled_reason,
        order_type_id,
        note,
        customer_name,
        user_id,
        customer_id,
        delivery_location_id,
        shipping_method,
        shipping_total,
        shipping_tax,
        tax_id,
        delivery_tax,
        total_tax,
        cash_due,
        surcharge,
        contact_access_code,
        testing_order,
        cancelled_by_customer,
        payment_method,
        payment_mode,
        is_scheduled,
        is_table_order,
        tip,
        tip_percentage,
        dgpos_table_id,
        table_order_method_id,
        device_platform,
        order_delayed,
        unique_order_id,
        created_at,
        updated_at
    )
VALUES (
        ?,
        sqlc.arg (delivery_platform_id),
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
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP
    );

-- name: GetOrderById :one
SELECT
    sqlc.embed(o),
    t.code AS delivery_tax_code,
    t.rate AS delivery_tax_rate,
    0 + COALESCE(
        (
            SELECT cds.id
        FROM dg_pos_cash_drawer cd
        JOIN dg_pos_cash_drawer_sessions cds 
            ON cds.cash_drawer_id = cd.id
        WHERE cd.outlet_id = o.shop_id
          AND o.created_at >= cds.opened_at
          AND (cds.closed_at IS NULL OR o.created_at < cds.closed_at)
        ORDER BY cds.opened_at DESC
        LIMIT 1
        ),
        0
    ) AS shift_id,
    dp.name AS dp_name,
    p.id AS platform_id,
    p.name AS platform_name,
    p.logo AS platform_logo,
    tom.name AS table_order_method_name,
    os.payment_type AS session_payment_type
FROM
    `order` o
    LEFT JOIN delivery_platform dp ON o.platform_id = dp.id
    LEFT JOIN platform p ON dp.platform_id = p.id
    LEFT JOIN table_order_methods tom ON tom.id = o.table_order_method_id
    LEFT JOIN taxes_main t ON t.id = o.tax_id
    LEFT JOIN order_session os ON os.id = o.order_session_id
WHERE
    o.id = ?;

-- name: UpdateOrder :exec
UPDATE `order`
SET
    shop_id = ?,
    platform_id = sqlc.arg (delivery_platform_id),
    remote_order_id = ?,
    display_order_id = ?,
    delivery_date_time = ?,
    total_amount = ?,
    sub_total = ?,
    total_fee = ?,
    campaign_code = ?,
    discount = ?,
    voucher_discount = ?,
    discount_mode_applied = ?,
    discount_percentage_applied = ?,
    discount_type = ?,
    vouchers = ?,
    status = ?,
    cancelled_reason = ?,
    order_type_id = ?,
    note = ?,
    customer_name = ?,
    user_id = ?,
    customer_id = ?,
    delivery_location_id = ?,
    shipping_method = ?,
    shipping_total = ?,
    shipping_tax = ?,
    total_tax = ?,
    cash_due = ?,
    surcharge = ?,
    contact_access_code = ?,
    testing_order = ?,
    cancelled_by_customer = ?,
    payment_method = ?,
    is_scheduled = ?,
    is_table_order = ?,
    tip = ?,
    tip_percentage = ?,
    dgpos_table_id = ?,
    table_order_method_id = ?,
    device_platform = ?,
    order_delayed = ?,
    unique_order_id = ?,
    updated_at = CURRENT_TIMESTAMP
WHERE
    id = ?;

-- name: UpdateOrderStatus :exec
UPDATE `order`
SET
    status = ?,
    updated_at = CURRENT_TIMESTAMP
WHERE
    id = ?;

-- name: UpdateOrderPaymentMode :exec
UPDATE `order`
SET
    payment_mode = ?,
    updated_at = CURRENT_TIMESTAMP
WHERE
    id = ?;

-- name: UpdateOrderTableId :exec
UPDATE `order`
SET
    dgpos_table_id = ?,
    updated_at = CURRENT_TIMESTAMP
WHERE
    id = ?;

-- name: UpdateOrderCancelledReason :exec
UPDATE `order`
SET
    cancelled_reason = ?,
    updated_at = CURRENT_TIMESTAMP
WHERE
    id = ?;

-- name: UpdateOrderUserID :exec
UPDATE `order`
SET
    user_id = ?
WHERE
    id = ?;


-- name: GetOrders :many
SELECT
    sqlc.embed(o),
    0 + COALESCE(
        (
            SELECT cds.id
        FROM dg_pos_cash_drawer cd
        JOIN dg_pos_cash_drawer_sessions cds 
            ON cds.cash_drawer_id = cd.id
        WHERE cd.outlet_id = o.shop_id
          AND o.created_at >= cds.opened_at
          AND (cds.closed_at IS NULL OR o.created_at < cds.closed_at)
        ORDER BY cds.opened_at DESC
        LIMIT 1
        ),
        0
    ) AS cash_drawer_session_id,
    CASE WHEN payment_mode IS NULL THEN 'UNPAID' ELSE 'PAID' END AS payment_status,
    dp.platform_id AS dp_id,
    dp.name AS delivery_platform_name,
    p.name AS platform_name,
    p.logo AS platform_logo,
    tbl.table_orderings_id AS table_orderings_id,
    tom.name AS table_order_method_name,
    tbl.name AS table_name
FROM
    `order` o
    LEFT JOIN dg_pos_tables tbl ON tbl.id = o.dgpos_table_id
    LEFT JOIN table_order_methods tom ON tom.id = o.table_order_method_id
    JOIN delivery_platform dp ON o.platform_id = dp.id
    JOIN platform p ON dp.platform_id = p.id
WHERE
    o.status != 'TEMP'
    AND dp.platform_id IN (sqlc.slice (platform_ids))
    AND (
        sqlc.narg (apply_status) IS NULL
        OR o.status = sqlc.arg (status)
    )
    AND (
        sqlc.narg (apply_outlet_id) IS NULL
        OR o.shop_id = sqlc.arg (outlet_id)
    )
    AND (
        sqlc.narg (apply_start_date) IS NULL
        OR o.created_at >= sqlc.arg (start_date)
    )
    AND (
        sqlc.narg (apply_end_date) IS NULL
        OR o.created_at <= sqlc.arg (end_date)
    )
ORDER BY
    CASE
        WHEN sqlc.arg (sort_by) = 'asc' THEN o.id
    END ASC,
    CASE
        WHEN sqlc.arg (sort_by) = 'desc' THEN o.id
    END DESC;

-- name: GetOrdersForExport :many
WITH order_card_transaction_payment_type AS (
    SELECT
        type_id,
        GROUP_CONCAT(payment_type) AS payment_types
    FROM
        order_transaction
    WHERE
        type = 'ORDER' AND transaction_mode = 'CARD'
    GROUP BY
        type_id
)
SELECT
    sqlc.embed(o),
    0 + COALESCE(
        (
            SELECT cds.id
        FROM dg_pos_cash_drawer cd
        JOIN dg_pos_cash_drawer_sessions cds 
            ON cds.cash_drawer_id = cd.id
        WHERE cd.outlet_id = o.shop_id
          AND o.created_at >= cds.opened_at
          AND (cds.closed_at IS NULL OR o.created_at < cds.closed_at)
        ORDER BY cds.opened_at DESC
        LIMIT 1
        ),
        0
    ) AS cash_drawer_session_id,
    p.name AS platform_name,
    c.phone AS customer_contact_number,
    c.key_id_phone AS customer_key_id_contact_number,
    c.country_code AS customer_country_code,
    otp.payment_types AS payment_types
FROM
    `order` o
    JOIN delivery_platform dp ON o.platform_id = dp.id
    JOIN platform p ON dp.platform_id = p.id
    LEFT JOIN customers c ON o.customer_id = c.id
    LEFT JOIN order_card_transaction_payment_type otp ON otp.type_id = o.id
WHERE
    o.status != 'TEMP'
    AND p.id IN (sqlc.slice (platform_ids))
    AND (
        sqlc.narg (status) IS NULL
        OR o.status = sqlc.narg (status)
    )
    AND (
        sqlc.narg (outlet_id) IS NULL
        OR o.shop_id = sqlc.narg (outlet_id)
    )
    AND o.created_at >= sqlc.arg (start_date) AND o.created_at <= sqlc.arg (end_date)
GROUP BY o.id
ORDER BY
    CASE
        WHEN sqlc.arg (sort_by) = 'asc' THEN o.id
    END ASC,
    CASE
        WHEN sqlc.arg (sort_by) = 'desc' THEN o.id
    END DESC;

-- SELECT
--     o.*,
--     cds.id AS cash_drawer_session_id,
--     p.name AS platform_name,
--     c.phone AS customer_contact_number,
--     c.key_id_phone AS customer_key_id_contact_number,
--     c.country_code AS customer_country_code
-- FROM
--      `order` o
--     JOIN delivery_platform dp ON o.platform_id = dp.id
--     JOIN platform p ON dp.platform_id = p.id
--     JOIN dg_pos_cash_drawer cd ON cd.outlet_id = o.shop_id
--     JOIN dg_pos_cash_drawer_sessions cds ON cds.cash_drawer_id = cd.id
--     AND o.created_at BETWEEN cds.opened_at AND COALESCE(cds.closed_at, NOW())
--     LEFT JOIN customers c ON o.customer_id = c.id
-- WHERE
--     o.status != 'TEMP'
--     AND p.id IN (9,6)
--     AND o.status = 'COMPLETED'
--     AND o.shop_id = 2
--     AND o.created_at BETWEEN '2025-11-03 00:00:00' AND '2025-11-03 23:59:59'
-- ORDER BY
--     o.id DESC;

-- name: GetCustomerOrders :many
SELECT
    sqlc.embed(o),
    dp.platform_id AS dp_id,
    p.name AS platform_name,
    p.logo AS platform_logo
FROM
    `order` o
    LEFT JOIN delivery_platform dp ON o.platform_id = dp.id
    LEFT JOIN platform p ON dp.platform_id = p.id
WHERE
    o.customer_id = ?;

-- name: GetMobileAppOrdersCount :one
SELECT COUNT(o.id) AS mobile_app_orders
FROM `order` o
WHERE
    o.device_platform = 'mobile'
    AND o.customer_id = ?;

-- name: GetMobileAppReservationsCount :one
SELECT COUNT(tr.id) AS mobile_app_reservations
FROM table_reservation tr
WHERE
    tr.user_type = 'CUSTOMER'
    AND tr.device_platform = 'mobile'
    AND tr.customer_id = ?;