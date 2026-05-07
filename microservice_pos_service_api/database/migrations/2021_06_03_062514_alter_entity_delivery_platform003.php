<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class AlterEntityDeliveryPlatform003 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('entity_delivery_platform', function ($table) {
            $table->boolean('available')->default(1)->after('allergies');
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
            $table->dropColumn(['available']);
        });
    }
}
