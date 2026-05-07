CREATE TABLE `entity_delivery_platform` (
    `id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `entity_id` int DEFAULT NULL,
    `delivery_platform_id` int NOT NULL,
    `external_item_id` varchar(191) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `created_at` timestamp NULL DEFAULT NULL,
    `updated_at` timestamp NULL DEFAULT NULL,
    `plu` varchar(191) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `price` decimal(8, 2) DEFAULT NULL,
    `item_name` varchar(191) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `allergies` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
    `available` tinyint(1) NOT NULL DEFAULT '1',
    `available_from` datetime DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB AUTO_INCREMENT = 3 DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci