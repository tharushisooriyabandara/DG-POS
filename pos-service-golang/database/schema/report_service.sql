CREATE TABLE `report_service` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `dns` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `server_client_id` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `server_client_secret` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `bearer_token` text COLLATE utf8mb4_unicode_ci,
  `expire_in` datetime DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci