<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class CreateWebshopMenu extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::create('webshop_menu', function (Blueprint $table) {
            $table->bigIncrements('id');
            $table->integer('main_menu_id');
            $table->integer('delivery_platform_id');
            $table->integer('status')->nullable();
            $table->integer('outlet_id');
            $table->longText('menu')->nullable();
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
        Schema::dropIfExists('webshop_menu');
    }
}
