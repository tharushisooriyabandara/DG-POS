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
        Schema::table('item_prices', function ($table) {
            $table->boolean('has_bogo_offer')->default(0);
            $table->integer('bogo_category')->nullable();
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('item_prices', function ($table) {
            $table->dropColumn(['has_bogo_offer', 'bogo_category']);
        });
    }
};
