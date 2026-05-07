<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\Schema;

class DotNetChanges0003 extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::table('webshop_customer', function ($table) {
            $table->string('reset_token')->nullable();
            $table->string('status')->nullable();
            $table->rememberToken();

        });

    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::table('webshop_customer', function ($table) {
            $table->dropColumn(['reset_token', 'remember_token', 'status']);
        });

    }
}
