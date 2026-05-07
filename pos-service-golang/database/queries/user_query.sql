-- name: GetUsers :many
SELECT
    u.id,
    u.name,
    u.key_id_name,
    u.last_name,
    u.key_id_last_name,
    u.email,
    u.key_id_email,
    u.contact_no,
    u.key_id_contact_no,
    u.address,
    u.key_id_address,
    r.id AS role_id,
    r.name AS role_name
FROM
    users u
    LEFT JOIN user_roles ur ON u.id = ur.user_id
    LEFT JOIN roles r ON ur.role_id = r.id
    JOIN user_shop us ON us.user_id = u.id
    JOIN shop s ON s.id = us.shop_id
WHERE
    r.id IN (sqlc.slice (role_ids))
    AND s.code = sqlc.arg (outlet_code)
ORDER BY u.name;

-- name: GetUserByID :one
SELECT
    u.id,
    u.name,
    u.key_id_name,
    u.last_name,
    u.key_id_last_name,
    u.email,
    u.key_id_email,
    u.address,
    u.key_id_address,
    u.contact_no,
    u.key_id_contact_no,
    u.status,
    u.created_at,
    u.updated_at,
    r.id AS role_id,
    r.name AS role_name
FROM
    users u
    LEFT JOIN user_roles ur ON u.id = ur.user_id
    LEFT JOIN roles r ON ur.role_id = r.id
WHERE
    u.id = ?;

-- name: GetUserByOutletCodeAndEmail :one
SELECT u.id, u.pin, u.status, s.id AS outlet_id
FROM
    users u
    JOIN user_shop us ON us.user_id = u.id
    JOIN shop s ON us.shop_id = s.id
WHERE
    u.hashed_email = ?
    AND s.code = sqlc.arg (outlet_code);

-- name: UpdateUserPin :exec
UPDATE users
SET
    pin = ?
WHERE
    id = ?;
