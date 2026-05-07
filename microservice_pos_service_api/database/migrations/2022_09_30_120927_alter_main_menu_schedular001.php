<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class AlterMainMenuSchedular001 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('main_menu_schedular', function ($table) {
            $table->text('platform_ids')->nullable()->after('publishable_date');
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('main_menu_schedular', function ($table) {
            $table->dropColumn(['platform_ids']);
        });
    }
}
