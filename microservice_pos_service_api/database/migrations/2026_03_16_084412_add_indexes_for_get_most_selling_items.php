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
        Schema::table('entity_delivery_platform', function (Blueprint $table) {
            $table->index(['entity_id', 'delivery_platform_id'], 'entity_delivery_platform_entity_id_platform_id_index');
        });

        Schema::table('item_prices', function (Blueprint $table) {
            $table->index(['entity_item_id', 'delivery_platform_id', 'main_menu_id'], 'item_prices_entity_item_id_platform_id_menu_id_index');
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('entity_delivery_platform', function (Blueprint $table) {
            $table->dropIndex('entity_delivery_platform_entity_id_platform_id_index');
        });

        Schema::table('item_prices', function (Blueprint $table) {
            $table->dropIndex('item_prices_entity_item_id_platform_id_menu_id_index');
        });
    }
};
