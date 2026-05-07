<?php

namespace Database\Seeders;

use App\Http\Models\Configuration;
use App\Http\Services\Pos\EposService;
use App\microservice_delivergate_api\Models\Shop;
use Illuminate\Database\Seeder;

class TenderTypeSeeder extends Seeder
{
    /**
     * Run the database seeds.
     *
     * @return void
     */
    public function run()
    {
        $types = [
            [
                "Id" => 80193,
                "Name" => "UBER_EATS",
                "Description" => "Uber Eats orders",
                "ClassificationId" => 1,
            ],
            [
                "Id" => 80194,
                "Name" => "DELIVEROO",
                "Description" => "Deliveroo orders",
                "ClassificationId" => 1,
            ],
            [
                "Id" => 80195,
                "Name" => "WOOCOMMERCE",
                "Description" => "Woocommerce orders",
                "ClassificationId" => 1,
            ],
        ];
        $eposService = new EposService;
        $shops = Shop::all();

        foreach ($shops as $keyshop => $shop) {
            foreach ($types as $key => $type) {
                if (count(Configuration::where('key', $type['Name'])->get()) == 0) {
                    $response = $eposService->createTenderType([$type], $shop->id);
                    if ($response->getStatusCode() == 200) {
                        $response = (json_decode($response->getContent()))->data;
                        if (is_array($response) && count($response) > 0) {
                            $config = new Configuration;
                            $config->shop_id = $shop->id;
                            $config->key = $response[0]->Name;
                            $config->value = $response[0]->Id;
                            $config->save();
                        }
                    }
                }
            }
        }
    }
}
