CREATE TABLE `table_order_methods` (
    `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
    `shop_id` int UNSIGNED NOT NULL,
    `brand_id` int UNSIGNED NOT NULL,
    `name` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `localization_name` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `description` text COLLATE utf8mb4_unicode_ci,
    `status` tinyint(1) NOT NULL DEFAULT '1',
    `created_at` TIMESTAMP NULL DEFAULT NULL,
    `updated_at` TIMESTAMP NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
)
