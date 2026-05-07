-- name: GetDeliveryPlatforms :many
SELECT dp.*, p.name AS platform_name
FROM
    delivery_platform dp
    LEFT JOIN platform p ON dp.platform_id = p.id
    WHERE
        (
            sqlc.narg(outlet_id) IS NULL
            OR dp.outlet_id = sqlc.narg(outlet_id)
        )
        AND (
            sqlc.narg(brand_id) IS NULL
            OR dp.webshop_brand_id = sqlc.narg(brand_id)
        );

-- name: GetDeliveryPlatform :one
SELECT dp.*, p.name AS platform_name, wb.brand_name AS brand_name
FROM delivery_platform dp
LEFT JOIN platform p ON dp.platform_id = p.id
LEFT JOIN webshop_brand wb ON dp.webshop_brand_id = wb.id
WHERE
    outlet_id = ?
    AND webshop_brand_id = ?
    AND platform_id = ?;

-- name: GetDeliveryPlatformById :one
SELECT * FROM delivery_platform WHERE id = ?;