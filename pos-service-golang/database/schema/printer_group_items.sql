CREATE TABLE `printer_group_item` (
    `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
    `printer_group_id` int NOT NULL,
    `item_id` int NOT NULL,
    `created_at` TIMESTAMP NULL DEFAULT NULL,
    `updated_at` TIMESTAMP NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
)
