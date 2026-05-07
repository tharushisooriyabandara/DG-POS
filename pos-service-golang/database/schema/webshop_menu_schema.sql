CREATE TABLE `webshop_menu` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `main_menu_id` int NOT NULL,
  `submenu_id` int DEFAULT NULL,
  `platform_id` int DEFAULT NULL,
  `delivery_platform_id` int NOT NULL,
  `status` int DEFAULT NULL,
  `outlet_id` int NOT NULL,
  `day` varchar(191) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `from` varchar(191) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `to` varchar(191) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `menu` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `category_ids` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;