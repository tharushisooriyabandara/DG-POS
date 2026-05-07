CREATE TABLE `shop_fee` (
    `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
    `shop_id` int NOT NULL,
    `webshop_brand_id` int UNSIGNED NOT NULL DEFAULT '1',
    `platform` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT 'WEBSHOP',
    `type` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `fee_name` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `fee` decimal(8, 2) NOT NULL,
    `fee_type` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `status` tinyint(1) NOT NULL DEFAULT '0',
    `tax_id` int DEFAULT NULL,
    `mandatory` tinyint(1) NOT NULL DEFAULT '0',
    `created_at` TIMESTAMP NULL DEFAULT NULL,
    `updated_at` TIMESTAMP NULL DEFAULT NULL,
    `deleted_at` TIMESTAMP NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB AUTO_INCREMENT = 6 DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci
