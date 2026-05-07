<?php

use Illuminate\Support\Facades\DB;
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
            $table->dropColumn(['shop_id']);
            $table->dropColumn(['type']);
            $table->string('code')->after('name');
            $table->string('description')->nullable()->after('code');
        });

        DB::statement('ALTER TABLE taxes MODIFY id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY');
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('taxes', function ($table) {
            $table->integer('shop_id')->nullable()->after('id');
            $table->string('type')->nullable()->after('shop_id');
            $table->dropColumn(['code', 'description']);
        });

        DB::statement('ALTER TABLE taxes MODIFY id BIGINT UNSIGNED');
        DB::statement('ALTER TABLE taxes DROP PRIMARY KEY');
        DB::statement('ALTER TABLE taxes MODIFY id CHAR(36)');
    }
};

