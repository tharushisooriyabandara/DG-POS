<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

class AlterEntityDeliveryPlatform001 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('entity_delivery_platform', function ($table) {
            $table->decimal('price')->nullable();
        });
        DB::statement("ALTER TABLE `entity_delivery_platform` MODIFY entity_id INT NULL");
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('entity_delivery_platform', function ($table) {
            $table->integer('entity_id')->change();
            $table->dropColumn(['price']);
        });
    }
}
