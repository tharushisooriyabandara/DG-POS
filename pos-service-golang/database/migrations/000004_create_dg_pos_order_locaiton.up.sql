CREATE TABLE IF NOT EXISTS `dg_pos_order_location` (
    `id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `order_id` int NOT NULL,
    `first_name` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `last_name` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `email` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `country_code` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `phone` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `address_line_1` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `address_line_2` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `city` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `state` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `postcode` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `country` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `created_at` timestamp NULL DEFAULT NULL,
    `updated_at` timestamp NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
)