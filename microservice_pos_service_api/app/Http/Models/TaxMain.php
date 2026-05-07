<?php

namespace App\Http\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class TaxMain extends Model
{
    use HasFactory;

    protected $table = 'taxes_main';
    protected $guarded = [];

    public function taxRules()
    {
        return $this->hasMany('App\Http\Models\TaxRule', 'tax_id');
    }
}
