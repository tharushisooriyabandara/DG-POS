<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class CreateEntityDeliveryPlatformTable extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::create('entity_delivery_platform', function (Blueprint $table) {
            $table->bigIncrements('id');
            $table->string('type');
            $table->integer('entity_id');
            $table->integer('delivery_platform_id');
            $table->string('external_item_id');
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
        Schema::dropIfExists('entity_delivery_platform');
    }
}
