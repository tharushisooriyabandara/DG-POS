CREATE TABLE IF NOT EXISTS `dg_pos_tmp_payments` (
    `id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `type_id` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `type` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `payment_amount` int NOT NULL,
    `payment_mode` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `transaction_id` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `created_at` timestamp NULL DEFAULT NULL,
    `updated_at` timestamp NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;
