-- name: GetPrinterGroupsByItemID :many
SELECT 
  pg.*
FROM printer_group_item pgi
JOIN printer_group pg ON pg.id = pgi.printer_group_id
WHERE 
  pgi.item_id = ?;
