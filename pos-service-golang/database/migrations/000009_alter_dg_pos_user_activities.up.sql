ALTER TABLE `dg_pos_user_activities`
ADD `brand_id` bigint UNSIGNED DEFAULT NULL AFTER `id`,
ADD `shop_id` bigint UNSIGNED DEFAULT NULL AFTER `brand_id`;