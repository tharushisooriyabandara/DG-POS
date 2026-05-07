<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class CreateModifierGroupTable extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::create('modifier_group', function (Blueprint $table) {
            $table->bigIncrements('id');
            $table->string('title');
            $table->integer('min_permitted')->nullable();
            $table->integer('max_permitted')->nullable();
            $table->integer('default_quantity')->nullable();
            $table->integer('charge_above')->nullable();
            $table->integer('refund_under')->nullable();
            $table->integer('sub_title')->nullable();
            $table->text('description')->nullable();
            $table->boolean('status')->default(1);
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
        Schema::dropIfExists('modifier_group');
    }
}
