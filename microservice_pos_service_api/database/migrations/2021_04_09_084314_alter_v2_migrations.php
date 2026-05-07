<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class AlterV2Migrations extends Migration
{
    public function up()
    {
        Schema::table('pos', function ($table) {
            $table->integer('shop_id')->after('id');
        });

        Schema::table('configuration', function ($table) {
            $table->integer('shop_id')->nullable()->after('id');
        });

        Schema::table('entity_delivery_platform', function ($table) {
            $table->dropColumn(['type']);
        });

        Schema::table('item', function ($table) {
            $table->dropColumn(['handle', 'reference_id', 'track_stock', 'sold_by_weight', 'is_composite', 'use_production', 'form', 'color', 'available_for_sale', 'variant_id', 'store_id', 'cost', 'reference_variant_id', 'barcode', 'purchase_cost', 'default_pricing_type', 'default_price']);
        });

        Schema::create('shop_main_menu', function (Blueprint $table) {
            $table->bigIncrements('id');
            $table->integer('shop_id');
            $table->integer('main_menu_id');
            $table->timestamps();
        });

        Schema::create('pos_items', function (Blueprint $table) {
            $table->bigIncrements('id');
            $table->integer('pos_id');
            $table->integer('shop_id');
            $table->integer('item_id_id');
            $table->integer('pos_item_id');
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
            $table->uuid('reference_variant_id')->nullable();
            $table->string('barcode')->nullable();
            $table->decimal('purchase_cost', 8, 2)->nullable();
            $table->string('default_pricing_type')->nullable();
            $table->decimal('default_price', 8, 2)->nullable();
            $table->timestamps();
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
            $table->dropColumn(['shop_id']);
        });

        Schema::table('configuration', function ($table) {
            $table->dropColumn(['shop_id']);
        });

        Schema::table('entity_delivery_platform', function ($table) {
            $table->string('type');
        });

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
            $table->uuid('reference_variant_id')->nullable();
            $table->string('barcode')->nullable();
            $table->decimal('purchase_cost', 8, 2)->nullable();
            $table->string('default_pricing_type')->nullable();
            $table->decimal('default_price', 8, 2)->nullable();
        });

        Schema::dropIfExists('shop_main_menu');
        Schema::dropIfExists('pos_items');
    }
}
