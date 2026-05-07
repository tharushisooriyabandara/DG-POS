-- name: InsertDgPosTmpPayment :exec
INSERT INTO
    dg_pos_tmp_payments (
        `type_id`,
        `type`,
        payment_mode,
        payment_amount,
        transaction_id,
        created_at,
        updated_at
    )
VALUES (
        ?,
        ?,
        ?,
        ?,
        ?,
        CURRENT_TIMESTAMP,
        CURRENT_TIMESTAMP
    );

-- name: GetDgPosTmpPayments :many
SELECT * FROM dg_pos_tmp_payments WHERE type_id = ?;

-- name: DeleteDgPosTmpPayment :exec
DELETE FROM dg_pos_tmp_payments WHERE type_id = ?;
