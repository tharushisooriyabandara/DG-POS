ALTER TABLE `dg_pos_payments`
MODIFY `payment_method_id` varchar(191) COLLATE utf8mb4_unicode_ci NOT NULL,
DROP COLUMN `payment_type`;