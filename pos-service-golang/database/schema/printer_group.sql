CREATE TABLE `printer_group` (
    `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
    `brand_id` int NOT NULL,
    `shop_id` int NOT NULL,
    `name` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `description` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `status` tinyint(1) NOT NULL DEFAULT '1',
    `created_at` TIMESTAMP NULL DEFAULT NULL,
    `updated_at` TIMESTAMP NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
)
