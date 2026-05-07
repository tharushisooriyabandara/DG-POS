<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

class AlterPivotTypes extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        DB::statement("ALTER TABLE `item_prices` MODIFY entity_item_id varchar(191) NOT NULL");
        DB::statement("ALTER TABLE `modifier_group_item` MODIFY item_id varchar(191) NOT NULL");
        DB::statement("ALTER TABLE `modifier_group_modifier_item` MODIFY item_id varchar(191) NOT NULL");
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('item_prices', function ($table) {
            $table->integer('entity_item_id')->change();
        });
        Schema::table('modifier_group_item', function ($table) {
            $table->integer('item_id')->change();
        });
        Schema::table('modifier_group_modifier_item', function ($table) {
            $table->integer('item_id')->change();
        });
    }
}
