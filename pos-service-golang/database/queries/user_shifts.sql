-- name: CreateLogin :exec
INSERT INTO dg_pos_user_shifts (user_id, login) VALUES (?, ?);

-- name: SetLogout :exec
UPDATE dg_pos_user_shifts
SET
    logout = ?
WHERE
    user_id = ?
    AND logout IS NULL;

-- name: GetActiveShift :one
SELECT *
FROM dg_pos_user_shifts
WHERE
    user_id = ?
    AND logout IS NULL;
