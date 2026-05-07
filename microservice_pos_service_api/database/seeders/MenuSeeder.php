<?php

namespace Database\Seeders;

use DateTime;
use Exception;
use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;

class MenuSeeder extends Seeder
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
                ['id' => '1', 'title' => 'All day', 'sub_title' => 'All day', 'description' => 'Breakfast items', 'image_url' => null, 'from' => '00:00:00', 'to' => '23:59:59', 'available_days' => 'a:6:{i:0;s:6:"monday";i:1;s:7:"tuesday";i:2;s:9:"wednesday";i:3;s:8:"thursday";i:4;s:6:"friday";i:5;s:8:"saturday";}', 'status' => 1, 'service_availability' => 'a:7:{i:0;a:2:{s:11:\"day_of_week\";s:6:\"monday\";s:12:\"time_periods\";a:1:{i:0;a:2:{s:10:\"start_time\";s:5:\"08:00\";s:8:\"end_time\";s:5:\"23:00\";}}}i:1;a:2:{s:11:\"day_of_week\";s:7:\"tuesday\";s:12:\"time_periods\";a:1:{i:0;a:2:{s:10:\"start_time\";s:5:\"08:00\";s:8:\"end_time\";s:5:\"23:00\";}}}i:2;a:2:{s:11:\"day_of_week\";s:9:\"wednesday\";s:12:\"time_periods\";a:1:{i:0;a:2:{s:10:\"start_time\";s:5:\"08:00\";s:8:\"end_time\";s:5:\"23:00\";}}}i:3;a:2:{s:11:\"day_of_week\";s:8:\"thursday\";s:12:\"time_periods\";a:1:{i:0;a:2:{s:10:\"start_time\";s:5:\"08:00\";s:8:\"end_time\";s:5:\"23:00\";}}}i:4;a:2:{s:11:\"day_of_week\";s:6:\"friday\";s:12:\"time_periods\";a:1:{i:0;a:2:{s:10:\"start_time\";s:5:\"08:00\";s:8:\"end_time\";s:5:\"23:00\";}}}i:5;a:2:{s:11:\"day_of_week\";s:8:\"saturday\";s:12:\"time_periods\";a:1:{i:0;a:2:{s:10:\"start_time\";s:5:\"08:00\";s:8:\"end_time\";s:5:\"21:00\";}}}i:6;a:2:{s:11:\"day_of_week\";s:6:\"sunday\";s:12:\"time_periods\";a:1:{i:0;a:2:{s:10:\"start_time\";s:5:\"08:00\";s:8:\"end_time\";s:5:\"21:00\";}}}}', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            ];
            DB::table('menu')->insert($menu);
        } catch (Exception $e) {
            Log::error($e->getMessage() . "Failed to add menu to system");
        }
    }
}
