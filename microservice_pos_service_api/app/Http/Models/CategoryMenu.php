<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class CategoryMenu extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'category_menu';
    protected $guarded = [];

    public function mainMenu()
    {
        return $this->belongsTo('App\Http\Models\MainMenu', 'main_menu_id', 'id');
    }
}
