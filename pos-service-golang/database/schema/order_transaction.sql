CREATE TABLE order_transaction (
    id bigint unsigned NOT NULL AUTO_INCREMENT,
    type_id bigint unsigned NOT NULL,
    `type` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    transaction_type varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    transaction_amount int NOT NULL,
    transaction_mode varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    payment_type varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    platform varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
    created_at timestamp NULL DEFAULT NULL,
    updated_at timestamp NULL DEFAULT NULL,
    PRIMARY KEY (id)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci
