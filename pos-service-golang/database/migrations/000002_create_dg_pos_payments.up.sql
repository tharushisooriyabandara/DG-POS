CREATE TABLE IF NOT EXISTS `dg_pos_payments` (
    `id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `order_id` int NOT NULL,
    `date_time` datetime DEFAULT NULL,
    `amount` decimal(12, 2) NOT NULL DEFAULT '0.00',
    `cash` decimal(12, 2) NOT NULL DEFAULT '0.00',
    `balance` decimal(12, 2) NOT NULL DEFAULT '0.00',
    `transaction_id` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `refund_id` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `payment_method_id` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `status` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `is_refund` tinyint(1) NOT NULL DEFAULT '0',
    `created_at` timestamp NULL DEFAULT NULL,
    `updated_at` timestamp NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
)