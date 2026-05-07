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
        Schema::table('taxes', function ($table) {
            $table->integer('shop_id')->nullable()->after('id');
            $table->string('type')->nullable()->after('shop_id');
            $table->dropColumn(['code', 'description']);
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('taxes', function ($table) {
            $table->dropColumn(['shop_id', 'type']);
            $table->string('code')->after('name');
            $table->string('description')->nullable()->after('code');
        });
    }
};
