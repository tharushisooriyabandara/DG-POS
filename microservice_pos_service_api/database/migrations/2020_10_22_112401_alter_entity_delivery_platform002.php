<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class AlterEntityDeliveryPlatform002 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('entity_delivery_platform', function ($table) {
            $table->string('item_name')->nullable();
            $table->text('allergies')->nullable();
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('entity_delivery_platform', function ($table) {
            $table->dropColumn(['item_name', 'allergies']);
        });
    }
}
