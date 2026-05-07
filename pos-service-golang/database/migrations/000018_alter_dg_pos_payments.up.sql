-- change current transaction_id column name to card_transaction_token
-- add new column transaction_id
ALTER TABLE `dg_pos_payments`
CHANGE COLUMN `transaction_id` `card_transaction_token` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
ADD COLUMN `transaction_id` bigint unsigned DEFAULT NULL AFTER `card_transaction_token`;
