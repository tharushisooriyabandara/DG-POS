<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class Shop extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'shop';
    protected $guarded = [];

    public function mainMenus()
    {
        return $this->belongsToMany('App\Http\Models\MainMenu', 'shop_main_menu', 'shop_id', 'main_menu_id')->withTimestamps();
    }
    public function shopDistanceDeliveryCost()
    {
        return $this->hasMany('App\Http\Models\ShopDistanceDeliveryCost', 'shop_id');
    }
}
