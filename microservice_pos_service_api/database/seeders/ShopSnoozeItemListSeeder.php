<?php

namespace Database\Seeders;

use DB;
use Config;
use Exception;
use App\Http\Models\Shop;
use Illuminate\Database\Seeder;
use App\Http\Services\MenuService;
use App\microservice_delivergate_api\Services\BaseService;

class ShopSnoozeItemListSeeder extends Seeder
{
    /**
     * Run the database seeds.
     *
     * @return void
     */
    public function run()
    {
        $menuService = new MenuService;
        $shops = Shop::all();
        foreach ($shops as $key => $shop) {
            try {
                $menuService->updateSnoozeItemListJson($shop->id);
            } catch (Exception $e) {
                $base_service = new BaseService;
                $base_service->loggerError($e, $this, __FUNCTION__, __LINE__, "Failed to update shop snooze item list. Shop ID - " . $shop->id);
            }
        }
    }
}
