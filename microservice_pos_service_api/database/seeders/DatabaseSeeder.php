<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;

class DatabaseSeeder extends Seeder
{
    /**
     * Seed the application's database.
     *
     * @return void
     */
    public function run()
    {
        //$this->call(DuplicateMenuSeeder::class);
        $this->call(MainMenuSeeder::class);
        //$this->call(DuplicateModifierSeeder::class);
        //$this->call(MainMenuSeeder::class);
        //$this->call(MenuItemIdSeeder::class);
        //$this->call(ShopSnoozeItemListSeeder::class); // run through developer dashboard. run once to update for existing shops.
        $this->call(TaxConditionTypeSeeder::class);
    }
}
