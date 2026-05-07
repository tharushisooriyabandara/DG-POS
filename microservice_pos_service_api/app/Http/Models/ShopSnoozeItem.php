<?php

namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Factories\HasFactory;

class ShopSnoozeItem extends Model
{
    use HasFactory;

    protected $table = 'shop_snooze_item';
    protected $guarded = [];

    public function shop()
    {
        return $this->belongsTo('App\microservice_delivergate_api\Models\Shop', 'outlet_id', 'id');
    }
}
