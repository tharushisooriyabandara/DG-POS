CREATE VIEW dg_pos_cash_drawer_expected_balances AS
SELECT
    cds.id AS cash_drawer_session_id,
    0 + SUM(CASE WHEN cdm.movement_type = 'PAY_IN' THEN cdm.amount ELSE 0 END) AS total_in_amount,
    0 + SUM(CASE WHEN cdm.movement_type = 'PAY_OUT' THEN cdm.amount ELSE 0 END) AS total_out_amount,
    0 + SUM(CASE WHEN cdm.movement_type = 'SALE' THEN cdm.amount ELSE 0 END) AS total_sales_amount,
    0 + SUM(CASE WHEN cdm.movement_type = 'REFUND' THEN cdm.amount ELSE 0 END) AS total_refund_amount,
    0 + COALESCE(SUM(cdm.amount), 0) AS total_movements,
    cds.opening_balance + COALESCE(SUM(cdm.amount), 0) AS expected_balance,
    cds.closing_balance_counted - (cds.opening_balance + COALESCE(SUM(cdm.amount), 0)) AS difference
FROM
    dg_pos_cash_drawer_sessions cds
    LEFT JOIN dg_pos_cash_movements cdm ON cds.id = cdm.cash_drawer_session_id
GROUP BY
    cds.id;