CREATE TABLE order_refund (
    id bigint unsigned NOT NULL AUTO_INCREMENT,
    type_id bigint unsigned NOT NULL,
    `type` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    transaction_id bigint unsigned NOT NULL,
    refund_amount int NOT NULL,
    refund_mode varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    reason text COLLATE utf8mb4_unicode_ci,
    created_at timestamp NULL DEFAULT NULL,
    updated_at timestamp NULL DEFAULT NULL,
    PRIMARY KEY (id)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci
