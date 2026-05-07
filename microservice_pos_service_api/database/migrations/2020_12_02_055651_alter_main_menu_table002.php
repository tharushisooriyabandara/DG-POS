<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

class AlterMainMenuTable002 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('main_menu', function ($table) {
            $table->text('item_ids')->nullable()->after('platform_ids');
            $table->integer('item_count')->default(0)->after('item_ids');
        });
        DB::statement("ALTER TABLE `main_menu` MODIFY status varchar(191) NOT NULL");
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('main_menu', function ($table) {
            $table->boolean('status')->change();
            $table->dropColumn(['item_ids', 'item_count']);
        });
    }
}
