CREATE TABLE `dg_pos_cash_drawer` (
    `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
    `outlet_id` bigint UNSIGNED NOT NULL,
    `is_active` tinyint(1) NOT NULL DEFAULT '0',
    `created_at` TIMESTAMP NULL DEFAULT NULL,
    `updated_at` TIMESTAMP NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;
