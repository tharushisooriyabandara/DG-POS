<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class WebshopMenu extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'webshop_menu';
    protected $guarded = [];

    public function mainMenu()
    {
        return $this->belongsTo('App\Http\Models\MainMenu', 'main_menu_id', 'id');
    }
}
