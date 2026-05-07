<?php

namespace Database\Seeders;

use DateTime;
use App\Http\Models\Configuration;
use Exception;
use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;

class ConfigurationSeeder extends Seeder
{
    /**
     * Run the database seeds.
     *
     * @return void
     */
    public function run()
    {
        $configurations = [
            ['id' => 1, 'key' => 'store_status', 'value' => 'Online', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            ['id' => 2, 'key' => 'auto_accept', 'value' => 'TRUE', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            ['key' => 'STORE_NAME', 'value' => 'DELIVERGATE FOODS', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            ['key' => 'STORE_ADDRESS', 'value' => '123. Harps rd, UK', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            ['key' => 'STORE_BRANCH', 'value' => 'London', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
        ];

        foreach ($configurations as $key => $configuration) {
            if (Configuration::where('key', $configuration['key'])->count() == 0) {
                try {
                    DB::table('configuration')->insert($configuration);
                } catch (Exception $e) {
                    Log::error($e->getMessage() . "Failed to add configuration");
                }
            }
        }
    }
}
