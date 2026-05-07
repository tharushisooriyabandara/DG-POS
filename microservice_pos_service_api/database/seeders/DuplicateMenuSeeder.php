<?php

namespace Database\Seeders;

use App\Http\Models\Menu;
use App\Http\Models\MainMenu;
use Illuminate\Database\Seeder;
use App\Http\Models\CategoryMenu;
use App\Http\Models\ItemCategory;
use App\Http\Models\MainMenuMenu;
use Illuminate\Support\Facades\DB;
use App\microservice_delivergate_api\Services\BaseService;
use Exception;

class DuplicateMenuSeeder extends Seeder
{
    /**
     * Run the database seeds.
     *
     * @return void
     */
    public function run()
    {
        try {
            DB::transaction(function () {
                $menus = Menu::whereNull('main_menu_id')->get();
                $mainMenus = MainMenu::all();
                foreach ($mainMenus as $mmkey => $mainMenu) {
                    foreach ($menus as $mkey => $menu) {
                        $clonedMenu = $menu->replicate();
                        $clonedMenu->main_menu_id = $mainMenu->id;
                        $clonedMenu->save();

                        $mainMenuMenu = MainMenuMenu::where('main_menu_id', $mainMenu->id)->where('menu_id', $menu->id)->get()->first();
                        if (!is_null($mainMenuMenu)) {
                            $clonedMainMenuMenu = $mainMenuMenu->replicate();
                            $clonedMainMenuMenu->menu_id = $clonedMenu->id;
                            $clonedMainMenuMenu->save();
                        }

                        $categoryMenus = CategoryMenu::where('main_menu_id', $mainMenu->id)->where('menu_id', $menu->id)->get();
                        foreach ($categoryMenus as $cmkey => $categoryMenu) {
                            $clonedCategoryMenu = $categoryMenu->replicate();
                            $clonedCategoryMenu->menu_id = $clonedMenu->id;
                            $clonedCategoryMenu->save();
                        }
                    }
                }
                $menuIds = $menus->pluck('id')->toArray();
                Menu::whereIn('id', $menuIds)->delete();
                MainMenuMenu::whereIn('menu_id', $menuIds)->delete();
                CategoryMenu::whereIn('menu_id', $menuIds)->delete();
            });
        } catch (Exception $e) {
            $base_service = new BaseService;
            $base_service->loggerError($e, $this, __FUNCTION__, __LINE__, "Failed to add main menu to system");
        }
    }
}
