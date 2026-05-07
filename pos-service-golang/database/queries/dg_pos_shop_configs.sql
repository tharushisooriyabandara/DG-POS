-- name: UpsertShopConfig :exec
INSERT INTO
    dg_pos_shop_config (
        id,
        shop_id,
        brand_id,
        terminal_id,
        config_type,
        data
    )
VALUES (
        ?,
        ?,
        ?,
        ?,
        ?,
        ?
    )
ON DUPLICATE KEY UPDATE
    data = VALUES(data);

-- name: GetShopConfig :one
SELECT *
FROM dg_pos_shop_config
WHERE
    config_type = ?
    AND shop_id = ?
    AND brand_id = ?
    AND terminal_id = ?;
