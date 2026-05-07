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
        Schema::table('tax_rules', function ($table) {
            $table->dropColumn(['order_profile', 'apply_to_refund']);
        });
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('tax_rules', function ($table) {
            $table->string('order_profile')->after('name');
            $table->boolean('apply_to_refund')->default(0)->after('order_profile');
        });
    }
};
