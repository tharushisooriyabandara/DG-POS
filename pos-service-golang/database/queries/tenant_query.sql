-- name: GetTenantInfo :one
SELECT * FROM tenants WHERE x_tenant_code = ?;

-- name: GetTenants :many
SELECT * FROM tenants;