ALTER TABLE `dg_pos_menu_config`
RENAME TO `dg_pos_shop_config`,
ADD COLUMN `config_type` varchar(191) NOT NULL DEFAULT 'MENU' AFTER `terminal_id`;