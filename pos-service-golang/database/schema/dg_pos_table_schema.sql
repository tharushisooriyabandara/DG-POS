CREATE TABLE `dg_pos_tables` (
    `id` BIGINT unsigned NOT NULL AUTO_INCREMENT,
    `table_orderings_id` bigint UNSIGNED DEFAULT NULL,
    `shop_id` int NOT NULL,
    `brand_id` int NOT NULL,
    `name` VARCHAR(191) NOT NULL,
    `description` VARCHAR(191) DEFAULT NULL,
    `seat_count` INT NOT NULL,
    `status` VARCHAR(191) NOT NULL,
    `ongoing_order_id` BIGINT UNSIGNED DEFAULT NULL,
    `created_at` TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at` TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`)
)