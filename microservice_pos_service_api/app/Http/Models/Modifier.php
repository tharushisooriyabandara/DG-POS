<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class Modifier extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'modifier';
    protected $guarded = [];

    public function options()
    {
        return $this->hasMany('App\Http\Models\ModifierOption', 'modifier_id');
    }

    public function modifier()
    {
        return $this->belongsTo('App\Http\Models\ModifierGroup','modifier_group_id');
    }

    public function shop()
    {
        return $this->belongsTo('App\microservice_delivergate_api\Models\Shop','shop_id');
    }
}
