<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class ModifierGroup extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'modifier_group';
    protected $guarded = [];

    public function items()
    {
        return $this->hasMany('App\Http\Models\ModifierGroupModifierItem', 'modifier_group_id');
    }

    public function mainItems()
    {
        return $this->hasMany('App\Http\Models\ModifierGroupItem', 'modifier_group_id', 'id');
    }
}
