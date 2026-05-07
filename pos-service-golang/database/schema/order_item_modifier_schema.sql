CREATE TABLE `order_item_modifier` (
    `id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `order_item_id` int NOT NULL,
    `parent_modifier_id` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `sub_parent_modifier_id` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `modifier_id` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `modifier_group_name` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `modifier_option_id` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `modifier_option_name` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `amount` decimal(8, 2) NOT NULL DEFAULT '0.00',
    `quantity` decimal(8, 2) NOT NULL DEFAULT '0.00',
    `created_at` timestamp NULL DEFAULT NULL,
    `updated_at` timestamp NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB AUTO_INCREMENT = 12610 DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci