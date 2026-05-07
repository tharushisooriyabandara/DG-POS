ALTER TABLE `dg_pos_payments`
CHANGE COLUMN `card_transaction_token` `transaction_id` varchar(191) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
DROP COLUMN `transaction_id`;
