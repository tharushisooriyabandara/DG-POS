CREATE TABLE `vouchers` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `campaign_code` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `voucher_type` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'common',
  `title` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `voucher_code` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
  `expirey_date` datetime NOT NULL,
  `start_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `value` decimal(8,2) NOT NULL,
  `value_type` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
  `current_usage` int NOT NULL DEFAULT '0',
  `status` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
  `webshop_brand_id` int unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `vouchers_voucher_code_unique` (`voucher_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci