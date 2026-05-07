CREATE TABLE `dg_pos_cash_movements` (
    `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
    `cash_drawer_session_id` bigint UNSIGNED NOT NULL,
    `movement_type` VARCHAR(191) NOT NULL,
    `amount` INT NOT NULL DEFAULT 0,
    `user_id` bigint UNSIGNED NOT NULL,
    `created_at` TIMESTAMP NULL DEFAULT NULL,
    `updated_at` TIMESTAMP NULL DEFAULT NULL,
    PRIMARY KEY (`id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;