<?php

namespace Database\Seeders;

use DB;
use DateTime;
use Exception;
use App\Http\Models\MainMenu;
use Illuminate\Database\Seeder;
use App\Http\Models\ModifierGroup;
use Illuminate\Support\Facades\Log;
use App\Http\Models\ModifierGroupItem;
use App\Http\Models\ModifierGroupModifierItem;

class DuplicateModifierSeeder extends Seeder
{
    /**
     * Run the database seeds.
     *
     * @return void
     */
    public function run()
    {
        $modifierGroups = ModifierGroup::all();
        $mainMenus = MainMenu::all();
        DB::transaction(function () use ($modifierGroups, $mainMenus) {
            foreach ($mainMenus as $key => $mainMenu) {
                foreach ($modifierGroups as $key1 => $modifierGroup) {
                    $newModifier = $modifierGroup->replicate();;
                    $newModifier->main_menu_id = $mainMenu->id;
                    $newModifier->save();

                    $modifierGroupItems = ModifierGroupItem::where('modifier_group_id', $modifierGroup->id)->get();
                    foreach ($modifierGroupItems as $key3 => $modifierGroupItem) {
                        $newModifierGroupItem = $modifierGroupItem->replicate();;
                        $newModifierGroupItem->modifier_group_id = $newModifier->id;
                        $newModifierGroupItem->save();
                    }

                    $modifierGroupModifierItems = ModifierGroupModifierItem::where('modifier_group_id', $modifierGroup->id)->get();
                    foreach ($modifierGroupModifierItems as $key4 => $modifierGroupModifierItem) {
                        $newModifierGroupModifierItem = $modifierGroupModifierItem->replicate();;
                        $newModifierGroupModifierItem->modifier_group_id = $newModifier->id;
                        $newModifierGroupModifierItem->save();
                    }

                }
            }
        });
    }
}
