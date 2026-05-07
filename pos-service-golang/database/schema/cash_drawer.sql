CREATE TABLE `dg_pos_cash_drawer` (
    `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
    `outlet_id` bigint UNSIGNED NOT NULL,
    `is_active` tinyint(1) NOT NULL DEFAULT '0',
    `created_at` TIMESTAMP NULL DEFAULT NULL,
    `updated_at` TIMESTAMP NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `dg_pos_cash_movements` (
    `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
    `cash_drawer_session_id` bigint UNSIGNED NOT NULL,
    `movement_type` VARCHAR(191) NOT NULL,
    `amount` INT NOT NULL DEFAULT 0,
    `note` VARCHAR(191) DEFAULT NULL,
    `user_id` bigint UNSIGNED NOT NULL,
    `created_at` TIMESTAMP NULL DEFAULT NULL,
    `updated_at` TIMESTAMP NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `dg_pos_cash_drawer_sessions` (
    `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
    `cash_drawer_id` bigint UNSIGNED NOT NULL,
    `session_started_user_id` bigint UNSIGNED NOT NULL,
    `session_ended_user_id` bigint UNSIGNED,
    `opened_at` TIMESTAMP NOT NULL,
    `opening_balance` INT NOT NULL DEFAULT 0,
    `closed_at` TIMESTAMP NULL DEFAULT NULL,
    `closing_balance_counted` INT DEFAULT 0,
    `closing_balance_expected` INT DEFAULT 0,
    `difference` INT DEFAULT 0,
    `total_in_amount` INT DEFAULT 0,
    `total_out_amount` INT DEFAULT 0,
    `total_sales_amount` INT DEFAULT 0,
    `total_other_sales_amount` INT DEFAULT 0,
    `total_refund_amount` INT DEFAULT 0,
    `status` VARCHAR(191) NOT NULL DEFAULT 'OPEN',
    `created_at` TIMESTAMP NULL DEFAULT NULL,
    `updated_at` TIMESTAMP NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

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