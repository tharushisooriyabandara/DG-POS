<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class CreateVariantsStoresTable extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::create('variants_stores', function (Blueprint $table) {
            $table->bigIncrements('id');
            $table->bigInteger('variant_id')->nullable();;
            $table->uuid('store_id')->nullable();;
            $table->string('pricing_type')->nullable();;
            $table->decimal('price',8,2)->nullable();;
            $table->boolean('available_for_sale')->nullable();;
            $table->string('optimal_stock')->nullable();;
            $table->string('low_stock')->nullable();;
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
        Schema::dropIfExists('variants_stores');
    }
}
