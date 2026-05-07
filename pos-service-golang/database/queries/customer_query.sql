-- name: GetGuestCustomer :one
SELECT id, first_name, hashed_first_name, last_name, hashed_last_name
FROM customers
WHERE
    hashed_email = 'b2dba1e9d647b1efa184223800731c18ddae80ddf38ca728b138ef59ff872be0';

-- name: GetCustomersWithAddresses :many
SELECT sqlc.embed(c), dl.*
FROM
    customers c
    LEFT JOIN delivery_location dl ON c.id = dl.customer_id
WHERE
    c.deleted_at IS NULL
ORDER BY c.id DESC;

-- name: CreateCustomer :execlastid
INSERT INTO
    customers (
        first_name,
        hashed_first_name,
        key_id_first_name,
        last_name,
        hashed_last_name,
        key_id_last_name, 
        phone,
        hashed_phone,
        key_id_phone,
        country_code,
        `type`,
        device_platform,
        status,
        account_brand,
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
        ?,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP
    );

-- name: GetCustomerByPhone :one
SELECT *
FROM customers
WHERE
    hashed_phone IN (
        sqlc.slice (phone_number_formats)
    )
    AND country_code = ?;

-- name: GetCustomerByID :one
SELECT * FROM customers WHERE id = ?;

-- name: UpdateCustomer :exec
UPDATE customers
SET
    first_name = ?,
    hashed_first_name = ?,
    key_id_first_name = ?,
    last_name = ?,
    hashed_last_name = ?,
    key_id_last_name = ?,
    hashed_phone = ?,
    phone = ?,
    key_id_phone = ?,
    country_code = ?,
    updated_at = CURRENT_TIMESTAMP
WHERE
    id = ?;
