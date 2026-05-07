<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class AlterItemTable001 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('item', function ($table) {
            DB::statement('ALTER TABLE item MODIFY COLUMN image_url TEXT');
        });
        Schema::table('entity_delivery_platform', function ($table) {
            $table->string('plu')->nullable();
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
            $table->dropColumn(['plu']);
        });
    }
}
