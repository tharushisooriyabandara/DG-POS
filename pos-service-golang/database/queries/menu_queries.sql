-- name: GetCategoryItemsByMenuIdAndShopId :one
SELECT *
FROM webshop_menu wm
WHERE
    wm.status = 1
    AND wm.main_menu_id = sqlc.arg (main_menu_id)
    AND wm.submenu_id IN (
        SELECT menu_id
        FROM main_menu_menu mmm
        WHERE
            mmm.main_menu_id = sqlc.arg (main_menu_id)
    )
    AND wm.delivery_platform_id = ?
    AND wm.outlet_id = ?
    AND (
        sqlc.arg (day) IS NULL
        OR wm.day = sqlc.arg (day)
    )
    AND (
        sqlc.arg (time_from) IS NULL
        OR wm.from <= sqlc.arg (time_from)
    )
    AND (
        sqlc.arg (time_to) IS NULL
        OR wm.to >= sqlc.arg (time_to)
    )
ORDER BY wm.id DESC;