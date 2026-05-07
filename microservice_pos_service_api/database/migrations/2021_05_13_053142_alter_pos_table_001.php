<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class AlterPosTable001 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('pos', function ($table) {
            $table->string('platform_id')->nullable()->after('id');
            $table->string('franchise_id')->nullable()->after('platform_id');
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('pos', function ($table) {
            $table->dropColumn(['platform_id', 'franchise_id']);
        });
    }
}
