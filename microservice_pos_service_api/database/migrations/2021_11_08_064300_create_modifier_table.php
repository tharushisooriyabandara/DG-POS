<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class CreateModifierTable extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::create('modifier', function (Blueprint $table) {
            $table->bigIncrements('id');
            $table->integer('modifier_group_id')->nullable();
            $table->integer('shop_id');
            $table->string('source_type');
            $table->integer('source_type_id')->nullable();
            $table->string('remote_id')->nullable();
            $table->string('title')->nullable();
            $table->string('sub_title')->nullable();
            $table->text('description')->nullable();
            $table->integer('min_permitted')->default(0);
            $table->integer('max_permitted')->default(0);
            $table->integer('default_quantity')->default(0);
            $table->integer('charge_above')->default(0);
            $table->integer('refund_under')->default(0);
            $table->boolean('status')->default(0);
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
        Schema::dropIfExists('modifier');
    }
}
