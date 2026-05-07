CREATE TABLE `webshop_brand` (
  `id` int unsigned NOT NULL AUTO_INCREMENT,
  `brand_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `brand_code` varchar(8) COLLATE utf8mb4_unicode_ci NOT NULL,
  `domain` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `status` tinyint(1) NOT NULL,
  `setup_status` tinyint(1) NOT NULL DEFAULT '0',
  `has_multishop` tinyint(1) NOT NULL DEFAULT '0',
  `default_shop` int DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL,
  `loyalty_points_per_pound` decimal(8,2) NOT NULL DEFAULT '0.00',
  `pounds_per_point` decimal(8,2) NOT NULL DEFAULT '0.00',
  `loyalty_expire_date` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `webshop_brand_brand_code_unique` (`brand_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci