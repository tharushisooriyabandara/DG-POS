<?php

namespace Database\Seeders;

use DateTime;
use App\Http\Models\TaxConditionType;
use Illuminate\Database\Console\Seeds\WithoutModelEvents;
use Illuminate\Database\Seeder;

class TaxConditionTypeSeeder extends Seeder
{
    /**
     * Run the database seeds.
     *
     * @return void
     */
    public function run()
    {
        $taxConditionTypes = [
            ['id' => 1, 'name' => 'category', 'description' => 'Select the category for this tax rule', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            ['id' => 2, 'name' => 'temperature', 'description' => 'Select the temperature (Hot/Cold) for this tax rule', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            ['id' => 3, 'name' => 'order_profile', 'description' => 'Select the order profile/shipping method for this tax rule', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            ['id' => 4, 'name' => 'apply_to_refund', 'description' => 'Select whether to apply this tax rule to refunds', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            ['id' => 5, 'name' => 'apply_next_matching_rule', 'description' => 'Select whether to continue to the next matching tax rule', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            //['id' => 6, 'name' => 'date_range', 'description' => 'Select the validity date range for this tax rule', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            //['id' => 7, 'name' => 'item_unit_price', 'description' => 'Select the unit price range for this tax rule', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            //['id' => 8, 'name' => 'line_price', 'description' => 'Select the line price range for this tax rule', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
            //['id' => 9, 'name' => 'receipt_total', 'description' => 'Select the receipt total range for this tax rule', 'created_at' => new DateTime(), 'updated_at' => new DateTime()],
        ];

        foreach ($taxConditionTypes as $key => $taxConditionType) {
            $type = TaxConditionType::firstOrNew(['id' => $taxConditionType['id']]);
            $type->name = $taxConditionType['name'];
            $type->description = $taxConditionType['description'];
            $type->save();
        }
    }
}
