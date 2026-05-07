-- name: InsertOrderTaxable :exec
INSERT INTO
    order_taxes (
        order_id,
        tax_rate,
        tax_code,
        tax_amount,
        taxable_amount,
        type,
        created_at,
        updated_at
    )
VALUES (?, ?, ?, ?, ?, ?, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);

-- name: GetOrderSalesTaxes :many
SELECT * FROM order_taxes WHERE order_id = ? AND type = 'SALE';

-- name: DeleteOrderTaxes :exec
DELETE FROM order_taxes WHERE order_id = ?;
