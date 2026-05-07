<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

class DotNetChanges extends Migration
{
    /**
     * Run the migrations.
     *
     * @return void
     */
    public function up()
    {
        Schema::create('tag', function (Blueprint $table) {
            $table->increments('id');
            $table->string('name', 200);
            $table->string('status', 100);
            $table->text('description')->nullable();
            $table->timestamps();
        });

        Schema::create('item_tag', function (Blueprint $table) {
            $table->increments('id');
            $table->integer('item_id');
            $table->integer('tag_id');
            $table->timestamps();
        });

        Schema::create('webshop_customer', function (Blueprint $table) {
            $table->increments('id');
            $table->string('first_name', 200);
            $table->string('last_name', 100)->nullable();
            $table->string('email', 200)->unique();
            $table->string('address', 200)->nullable();
            $table->string('contact_number', 200)->nullable();
            $table->string('password', 200)->nullable();
            $table->timestamps();
        });

        Schema::table('item_prices', function ($table) {
            $table->float('discount_amount', 8, 2)->default(0);
            $table->float('tax_percentage', 8, 2)->default(0);
            $table->string('discount_type', 100)->default("percentage");
        });

        DB::statement("ALTER TABLE `modifier_group` MODIFY sub_title varchar(200) NOT NULL");
        DB::statement("ALTER TABLE `order_items` MODIFY quantity FLOAT(8,2) NOT NULL");
    }

    /**
     * Reverse the migrations.
     *
     * @return void
     */
    public function down()
    {
        Schema::dropIfExists('tag');
        Schema::dropIfExists('item_tag');
        Schema::dropIfExists('webshop_customer');
    }
}
