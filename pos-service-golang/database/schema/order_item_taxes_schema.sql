CREATE TABLE `order_item_tax` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `order_id` int NOT NULL,
  `order_item_id` int NOT NULL,
  `order_item_modifier_id` int DEFAULT NULL,
  `item_price` int NOT NULL,
  `tax_id` int NOT NULL,
  `tax_profile_id` int NOT NULL,
  `tax_rule_id` int NOT NULL,
  `amount` int NOT NULL DEFAULT '0',
  `created_at` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT NULL,
  PRIMARY KEY (`id`)
);

