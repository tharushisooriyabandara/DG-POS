<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class ItemPrice extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'item_prices';
    protected $guarded = [];

    public function category()
    {
        return $this->belongsTo('App\Http\Models\Category', 'bogo_category', 'id');
    }
}
