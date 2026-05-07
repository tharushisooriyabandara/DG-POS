<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class ModifierGroupModifierItem extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'modifier_group_modifier_item';
    protected $guarded = [];

    public function modifier()
    {
        return $this->belongsTo('App\Http\Models\ModifierGroup', 'modifier_group_id', 'id');
    }

    public function item()
    {
        return $this->belongsTo('App\Http\Models\EntityDeliveryPlatform', 'item_id', 'external_item_id');
    }

    public function entityItem()
    {
        return $this->hasOne('App\Http\Models\EntityDeliveryPlatform', 'external_item_id', 'item_id')->where('delivery_platform_id', $this->platform);
    }

    public function itemPrimary()
    {
        return $this->belongsTo('App\Http\Models\Item', 'item_id', 'id');
    }
}
