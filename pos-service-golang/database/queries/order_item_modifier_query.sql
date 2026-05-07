-- name: CreateOrderItemModifier :execlastid
INSERT INTO
    order_item_modifier (
        order_item_id,
        parent_modifier_id,
        sub_parent_modifier_id,
        modifier_id,
        modifier_group_name,
        modifier_option_id,
        modifier_option_name,
        amount,
        quantity,
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
        ?
    );

-- name: DeleteOrderItemModifiers :exec
DELETE FROM order_item_modifier
WHERE
    order_item_id IN (sqlc.slice (order_item_ids));
