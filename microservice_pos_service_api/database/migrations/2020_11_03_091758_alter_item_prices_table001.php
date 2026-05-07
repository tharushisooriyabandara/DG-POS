<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

class AlterItemPricesTable001 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('item_prices', function ($table) {
            $table->dropColumn(['type', 'type_id']);
            $table->integer('main_menu_id')->after('id');
            $table->integer('entity_item_id')->after('main_menu_id');
        });
        DB::statement("ALTER TABLE `item_prices` MODIFY price DECIMAL(8,2) DEFAULT 0 NOT NULL");

    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('item_prices', function ($table) {
            $table->string('type')->nullable();
            $table->integer('type_id')->nullable();
            $table->dropColumn(['main_menu_id', 'entity_item_id']);
        });
    }
}
