-- name: CreateDeliveryLocation :execlastid
INSERT INTO
    delivery_location (
        customer_id,
        label,
        flat_no,
        house_no,
        address_line_1,
        address_line_2,
        latitude,
        longitude,
        city,
        landmark,
        postal_code,
        default_address,
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
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP
    );

-- name: UpdateDeliveryLocation :exec
UPDATE delivery_location
SET
    label = ?,
    flat_no = ?,
    house_no = ?,
    address_line_1 = ?,
    address_line_2 = ?,
    latitude = ?,
    longitude = ?,
    city = ?,
    landmark = ?,
    postal_code = ?,
    default_address = ?,
    updated_at = CURRENT_TIMESTAMP
WHERE
    id = ?
    AND customer_id = ?;

-- name: DeleteDeliveryLocation :exec
DELETE FROM delivery_location WHERE id = ?;

-- name: GetCustomerAddresses :many
SELECT *
FROM delivery_location
WHERE
    customer_id = ?
    AND label IS NOT NULL
    AND address_line_1 IS NOT NULL;

-- name: MakeCustomerAddressNonDefault :exec
UPDATE delivery_location
SET
    default_address = 0
WHERE
    customer_id = ?;

-- name: GetOrderReceiver :one
SELECT sqlc.embed(dl), sqlc.embed(c)
FROM
    delivery_location dl
    JOIN customers c ON dl.customer_id = c.id
WHERE
    dl.id = ?
    AND customer_id = ?;