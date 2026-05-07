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
        Schema::create('tax_rule_conditions', function (Blueprint $table) {
            $table->id();
            $table->integer('tax_rule_id');
            $table->string('condition_type');
            $table->string('condition_value')->nullable();
            $table->decimal('min_value')->nullable();
            $table->decimal('max_value')->nullable();
            $table->datetime('start_date')->nullable();
            $table->datetime('end_date')->nullable();
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
        Schema::dropIfExists('tax_rule_conditions');
    }
};
