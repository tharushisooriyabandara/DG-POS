<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class PosCategory extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'pos_categories';
    protected $guarded = [];

    public function category()
    {
        return $this->belongsTo('App\Http\Models\Category', 'category_id');
    }
}
