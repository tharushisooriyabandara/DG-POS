<?php

namespace Database\Seeders;

use DateTime;
use Exception;
use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;

class PosSeeder extends Seeder
{
    /**
     * Run the database seeds.
     *
     * @return void
     */
    public function run()
    {
        $poses = [
            ['id' => 1, 'name' => 'EPOS', 'parameters' => serialize(['EPOS_BASE_URL', 'EPOS_KEY', 'EPOS_SECRET', 'EPOS_AUTH_TOKEN']), 'parameter_values' => serialize(['EPOS_BASE_URL', 'EPOS_KEY', 'EPOS_SECRET', 'EPOS_AUTH_TOKEN']), 'status' => 1, 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
        ];

        foreach ($poses as $key => $pos) {
            try {
                DB::table('pos')->insert($pos);
            } catch (Exception $e) {
                Log::error($e->getMessage() . "Failed to add POS to system");
            }
        }
    }
}
