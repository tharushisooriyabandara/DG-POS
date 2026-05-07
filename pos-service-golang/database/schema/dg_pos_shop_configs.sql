CREATE TABLE `dg_pos_shop_config` (
  `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
  `shop_id` int NOT NULL,
  `brand_id` int NOT NULL,
  `terminal_id` int NOT NULL,
  `config_type` varchar(191) NOT NULL,
  `data` json NOT NULL,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
)
