<?php

namespace App\Http\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class TaxRule extends Model
{
    use HasFactory;

    protected $table = 'tax_rules';
    protected $guarded = [];

    public function taxProfile()
    {
        return $this->belongsTo('App\Http\Models\TaxProfile', 'tax_profile_id');
    }

    public function tax()
    {
        return $this->belongsTo('App\Http\Models\TaxMain', 'tax_id');
    }

    public function taxRuleConditions()
    {
        return $this->hasMany('App\Http\Models\TaxRuleCondition', 'tax_rule_id');
    }
}
