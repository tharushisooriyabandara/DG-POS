-- name: CreateDgPosOrderLocation :exec
INSERT INTO
    `dg_pos_order_location` (
        order_id,
        first_name,
        last_name,
        email,
        country_code,
        country,
        phone,
        flat_no,
        house_no,
        address_line_1,
        address_line_2,
        city,
        landmark,
        postcode,
        latitude,
        longitude,
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
        ?,
        ?,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP
    );

-- name: GetDgPosOrderLocation :one
SELECT * FROM dg_pos_order_location WHERE order_id = ?;

-- name: GetWebshopOrderLocation :one
SELECT * FROM webshop_order_location WHERE order_id = ?;