<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('category', function ($table) {
            $table->boolean('is_bogo_category')->default(0);
            $table->integer('buy_quantity')->default(0);
            $table->integer('get_quantity')->default(0);
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('category', function ($table) {
            $table->dropColumn(['is_bogo_category', 'buy_quantity', 'get_quantity']);
        });
    }
};
