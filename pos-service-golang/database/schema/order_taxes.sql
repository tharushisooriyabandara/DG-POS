CREATE TABLE order_taxes (
    id bigint unsigned NOT NULL AUTO_INCREMENT,
    order_id int NOT NULL,
    tax_rate decimal(8, 2) NOT NULL,
    tax_code varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    tax_amount int NOT NULL,
    taxable_amount int NOT NULL,
    type varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
    created_at timestamp NULL DEFAULT NULL,
    updated_at timestamp NULL DEFAULT NULL,
    PRIMARY KEY (id)
)
