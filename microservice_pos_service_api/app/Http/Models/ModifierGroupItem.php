<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class ModifierGroupItem extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'modifier_group_item';
    protected $guarded = [];

    public function modifier()
    {
        return $this->belongsTo('App\Http\Models\ModifierGroup', 'modifier_group_id', 'id');
    }

    public function item()
    {
        return $this->belongsTo('App\Http\Models\EntityDeliveryPlatform', 'external_item_id', 'item_id');
    }

    public function alternativeItem()
    {
        return $this->hasOne('App\Http\Models\EntityDeliveryPlatform', 'external_item_id', 'item_id');
    }
}
