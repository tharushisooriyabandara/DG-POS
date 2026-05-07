<?php

namespace App\Http\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class TaxRuleCondition extends Model
{
    use HasFactory;

    protected $table = 'tax_rule_conditions';
    protected $guarded = [];

    public function taxRule()
    {
        return $this->belongsTo('App\Http\Models\TaxRule', 'tax_rule_id');
    }
}
