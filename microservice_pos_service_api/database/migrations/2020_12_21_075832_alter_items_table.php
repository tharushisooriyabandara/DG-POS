<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\Schema;

class AlterItemsTable extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('item', function ($table) {
            $table->uuid('reference_variant_id')->nullable();
            $table->string('barcode')->nullable();
            $table->decimal('purchase_cost', 8, 2)->nullable();
            $table->string('default_pricing_type')->nullable();
            $table->decimal('default_price', 8, 2)->nullable();
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('item', function ($table) {
            $table->dropColumn(['reference_variant_id', 'barcode', 'purchase_cost', 'default_pricing_type', 'default_price']);
        });
    }
}
