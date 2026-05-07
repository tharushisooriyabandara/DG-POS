CREATE TABLE `order_shop_fee` (
    `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
    `order_id` int NOT NULL,
    `shop_fee_id` int NOT NULL,
    `amount` int NOT NULL,
    `shop_fee_tax` int DEFAULT NULL,
    `tax_id` int DEFAULT NULL,
    `created_at` TIMESTAMP NULL DEFAULT NULL,
    `updated_at` TIMESTAMP NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB AUTO_INCREMENT = 10879 DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci
