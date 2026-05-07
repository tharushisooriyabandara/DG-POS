<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class ModifierOption extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'modifier_option';
    protected $guarded = [];

    public function modifier()
    {
        return $this->belongsTo('App\Http\Models\Modifier', 'modifier_id', 'id');
    }
}
