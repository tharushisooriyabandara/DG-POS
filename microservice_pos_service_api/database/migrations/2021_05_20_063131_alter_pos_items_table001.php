<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

class AlterPosItemsTable001 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('pos_items', function ($table) {
            $table->string('title')->nullable()->after('pos_item_id');
        });
        DB::statement("ALTER TABLE `pos_items` MODIFY pos_item_id varchar(191) NULL");
        DB::statement("ALTER TABLE `pos_items` MODIFY variant_id varchar(191) NULL");
        DB::statement("ALTER TABLE `pos_items` MODIFY store_id varchar(191) NULL");
        DB::statement("ALTER TABLE `pos_items` MODIFY reference_variant_id varchar(191) NULL");
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('pos_items', function ($table) {
            $table->dropColumn(['title']);
        });
    }
}
