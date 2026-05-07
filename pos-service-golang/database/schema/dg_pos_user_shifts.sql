CREATE TABLE IF NOT EXISTS `dg_pos_user_shifts` (
    `id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `user_id` bigint NOT NULL,
    `login` TIMESTAMP NOT NULL,
    `logout` TIMESTAMP DEFAULT NULL,
    `created_at` timestamp DEFAULT CURRENT_TIMESTAMP,
    `updated_at` timestamp DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;