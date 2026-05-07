CREATE TABLE IF NOT EXISTS `dg_pos_menu_config` (
  `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
  `shop_id` int NOT NULL,
  `brand_id` int NOT NULL,
  `terminal_id` int NOT NULL,
  `data` json NOT NULL,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
)
