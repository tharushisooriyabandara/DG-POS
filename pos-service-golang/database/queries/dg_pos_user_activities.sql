-- name: CreateActivityLog :exec
INSERT INTO
    `dg_pos_user_activities` (
        brand_id,
        shop_id,
        log_name,
        description,
        event,
        subject_id,
        causer_id
    )
VALUES (?, ?, ?, ?, ?, ?, ?);