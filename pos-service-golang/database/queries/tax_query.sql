-- name: GetTaxProfilesWithRulesConditions :many
SELECT * FROM tax_profiles tp
JOIN tax_rules tr ON tr.tax_profile_id = tp.id
JOIN tax_rule_conditions trc ON trc.tax_rule_id = tr.id
JOIN taxes_main t ON t.id = tr.tax_id;

-- name: GetTax :one
SELECT * FROM taxes_main WHERE id = ?;
