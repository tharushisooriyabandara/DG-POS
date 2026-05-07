<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class AlterWebshopMenu001 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('webshop_menu', function ($table) {
            $table->integer('submenu_id')->nullable()->after('main_menu_id');
            $table->string('day')->nullable()->after('outlet_id');
            $table->text('category_ids')->nullable()->after('menu');
            $table->time('from')->nullable()->after('day');
            $table->time('to')->nullable()->after('from');
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('webshop_menu', function ($table) {
            $table->dropColumn(['day', 'submenu_id', 'from', 'to', 'category_ids']);
        });
    }
}
