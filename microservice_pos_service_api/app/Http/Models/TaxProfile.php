<?php

namespace App\Http\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class TaxProfile extends Model
{
    use HasFactory;

    protected $table = 'tax_profiles';
    protected $guarded = [];

    public function taxRules()
    {
        return $this->hasMany('App\Http\Models\TaxRule', 'tax_profile_id');
    }
}
