<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class ItemCategory extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'item_category';
    protected $guarded = [];

    public function mainMenu()
    {
        return $this->belongsTo('App\Http\Models\MainMenu', 'main_menu_id', 'id');
    }
}
