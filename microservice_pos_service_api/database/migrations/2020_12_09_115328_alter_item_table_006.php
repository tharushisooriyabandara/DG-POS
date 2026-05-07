<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class AlterItemTable006 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('item', function ($table) {
            $table->string('handle')->nullable();
            $table->boolean('reference_id')->nullable();
            $table->boolean('track_stock')->nullable();
            $table->boolean('sold_by_weight')->nullable();
            $table->boolean('is_composite')->nullable();
            $table->boolean('use_production')->nullable();
            $table->string('form')->nullable();
            $table->string('color')->nullable();
            $table->boolean('available_for_sale')->nullable();
            $table->uuid('variant_id')->nullable();
            $table->uuid('store_id')->nullable();
            $table->decimal('cost')->default(0);
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('item', function ($table) {
            $table->dropColumn(['handle','reference_id','track_stock','sold_by_weight','is_composite','use_production','form','color','image_url','available_for_sale','variant_id','cost']);
        });
    }
}
