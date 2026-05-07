<?php

namespace Database\Seeders;

use DateTime;
use App\microservice_delivergate_api\Services\BaseService;
use Exception;
use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;

class MainMenuSeeder extends Seeder
{
    /**
     * Run the database seeds.
     *
     * @return void
     */
    public function run()
    {
        try {
            $menu = [
                ['id' => '1', 'name' => 'Main menu', 'description' => 'Main menu', 'platform_ids' => null, 'status' => 'ACTIVE', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            ];
            DB::table('main_menu')->insert($menu);
        } catch (Exception $e) {
            $base_service = new BaseService;
            $base_service->loggerError($e, $this, __FUNCTION__, __LINE__, "Failed to add main menu to system");
        }
    }
}
