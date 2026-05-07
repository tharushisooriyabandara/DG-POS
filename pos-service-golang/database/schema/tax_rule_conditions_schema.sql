CREATE TABLE `tax_rule_conditions` (
  `id` bigint UNSIGNED NOT NULL,
  `tax_rule_id` int NOT NULL,
  `condition_type` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
  `condition_value` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `min_value` decimal(8,2) DEFAULT NULL,
  `max_value` decimal(8,2) DEFAULT NULL,
  `start_date` datetime DEFAULT NULL,
  `end_date` datetime DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;