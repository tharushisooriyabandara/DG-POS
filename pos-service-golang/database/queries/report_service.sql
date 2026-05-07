-- name: GetLatestReportServiceToken :one
SELECT * FROM report_service ORDER BY id DESC LIMIT 1;

-- name: UpdateReportServiceToken :exec
UPDATE report_service
SET
    bearer_token = ?,
    expire_in = ?,
    updated_at = CURRENT_TIMESTAMP
WHERE
    id = ?;