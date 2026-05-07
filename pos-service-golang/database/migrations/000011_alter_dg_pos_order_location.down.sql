ALTER TABLE `dg_pos_order_location`
DROP COLUMN `flat_no`,
DROP COLUMN `house_no`,
DROP COLUMN `landmark`,
DROP COLUMN `latitude`,
DROP COLUMN `longitude`,
ADD COLUMN `state` varchar(191) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL AFTER `city`,
MODIFY COLUMN `city` varchar(191) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL;