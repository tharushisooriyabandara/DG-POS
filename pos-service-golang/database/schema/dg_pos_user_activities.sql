CREATE TABLE `dg_pos_user_activities` (
    `id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `brand_id` bigint unsigned DEFAULT NULL,
    `shop_id` bigint unsigned DEFAULT NULL,
    `log_name` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `description` text COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    `event` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    `subject_id` bigint NOT NULL,
    `causer_id` bigint NOT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;