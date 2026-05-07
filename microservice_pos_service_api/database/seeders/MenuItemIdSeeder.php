<?php

namespace Database\Seeders;

use App\Http\Models\Menu;
use Illuminate\Database\Seeder;

class MenuItemIdSeeder extends Seeder
{
    /**
     * Run the database seeds.
     *
     * @return void
     */
    public function run()
    {
        $menus = Menu::all();
        foreach ($menus as $key => $menu) {
            if (is_null($menu->item_ids) && !is_null($menu->mainMenu)) {
                $menu->item_ids = $menu->mainMenu->item_ids;
                $menu->item_count = $menu->mainMenu->item_count;
                $menu->save();
            }
        }
    }
}
