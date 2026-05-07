<?php
namespace App\Http\Models;

use App\Http\Models\Category;
use App\Http\Models\ItemCategory;
use Illuminate\Database\Eloquent\Model;

class Item extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'item';
    protected $guarded = [];

    public function categories()
    {
        return $this->belongsToMany('App\Http\Models\Category', 'item_category', 'item_id', 'category_id')->withPivot('main_menu_id')->withTimestamps();
    }

    public function categoriesByMainMenu($id)
    {
        $categories = ItemCategory::where('item_id', $this->id)->where('main_menu_id', $id)->pluck('category_id')->toArray();
        if (count($categories)==0) {
            //$categories = ItemCategory::where('item_id', $this->id)->pluck('category_id')->toArray();
        }
        return Category::whereIn('id', $categories)->get();
    }

    public function entityDeliveryPlatform()
    {
        return $this->hasMany('App\Http\Models\EntityDeliveryPlatform', 'entity_id');
        //return $this->hasOne('App\Http\Models\EntityDeliveryPlatform', 'entity_id')->where('type', 'ITEM');
    }

    public function prices()
    {
        return $this->hasMany('App\Http\Models\ItemPrice', 'entity_item_id');
    }

    public function posItems()
    {
        return $this->hasMany('App\Http\Models\PosItem', 'item_id_id');
    }

    public function mainMenus()
    {
        return $this->belongsToMany('App\Http\Models\MainMenu', 'item_category', 'item_id', 'main_menu_id')->withTimestamps();
    }

    public function customers()
    {
        return $this->belongsToMany('App\microservice_delivergate_api\Models\Customer', 'customer_favourite_item', 'item_id', 'customer_id');
    }

    public function images()
    {
        return $this->hasMany('App\Http\Models\Images', 'type_id')->where('type', 'ITEM');
    }

    public function printerGroups()
    {
        return $this->belongsToMany('App\Http\Models\PrinterGroup', 'printer_group_item', 'item_id', 'printer_group_id');
    }
}
