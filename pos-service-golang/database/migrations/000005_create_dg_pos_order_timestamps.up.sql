CREATE TABLE `dg_pos_order_status_timestamps` (
    `id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `order_id` int NOT NULL,
    `queue` datetime DEFAULT NULL,
    `preparing` datetime DEFAULT NULL,
    `served` datetime DEFAULT NULL,
    `delivered` datetime DEFAULT NULL,
    `completed` datetime DEFAULT NULL,
    `created_at` timestamp NULL DEFAULT NULL,
    `updated_at` timestamp NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci