<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

class DotNetChanges0005 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('webshop_customer', function (Blueprint $table) {
            $table->string('strip_id')->nullable();
        });

        Schema::table('item_prices', function (Blueprint $table) {
            $table->decimal('sale_price',8,2)->nullable();
            $table->boolean('is_sale')->default(false);
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('webshop_customer', function (Blueprint $table) {
            $table->dropColumn('strip_id');
        });

        Schema::table('item_prices', function (Blueprint $table) {
            $table->dropColumn('sale_price');
            $table->dropColumn('is_sale');
        });
    }
}
