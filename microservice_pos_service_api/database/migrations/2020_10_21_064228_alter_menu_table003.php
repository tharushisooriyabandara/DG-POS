<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class AlterMenuTable003 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('menu', function ($table) {
            $table->time('from')->default('00:00:00')->after('image_url');
            $table->time('to')->default('23:59:59')->after('from');
            $table->text('available_days')->nullable()->after('to');
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('menu', function ($table) {
            $table->dropColumn(['from', 'to', 'available_days']);
        });
    }
}
