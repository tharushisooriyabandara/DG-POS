-- name: CreateOrderShopFee :exec
INSERT INTO
    order_shop_fee (
        order_id,
        shop_fee_id,
        amount,
        shop_fee_tax,
        tax_id,
        created_at,
        updated_at
    )
VALUES (?, ?, ?, ?, ?, ?, ?);
