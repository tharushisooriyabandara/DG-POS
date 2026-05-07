<?php

namespace App\Http\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class TaxConditionType extends Model
{
    use HasFactory;

    protected $table = 'tax_condition_types';
    protected $guarded = [];
}
