<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class AlterModifierGruopItemAndModifierGruopModifierItemTables extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('modifier_group_item', function ($table) {
            $table->string('platform')->nullable();
        });

        Schema::table('modifier_group_modifier_item', function ($table) {
            $table->string('platform')->nullable();
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('modifier_group_item', function ($table) {
            $table->dropColumn(['platform']);
        });

        Schema::table('modifier_group_modifier_item', function ($table) {
            $table->dropColumn(['platform']);
        });
    }
}
