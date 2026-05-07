<?php
namespace App\Http\Models;

use App\Http\Models\Item;
use App\Http\Models\ItemCategory;
use Illuminate\Database\Eloquent\Model;
use App\Http\Models\EntityDeliveryPlatform;

class Category extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'category';
    protected $guarded = [];

    public function items()
    {
        return $this->belongsToMany('App\Http\Models\Item', 'item_category', 'category_id', 'item_id')->withTimestamps()->withPivot('main_menu_id');
    }

    public function entityPlatformItems()
    {
        $items = $this->items->pluck('id')->toArray();
        $entityItems = EntityDeliveryPlatform::whereIn('entity_id', $items)->get();
        return $entityItems;
    }

    public function itemList($main_menu)
    {
        $categoryItems = ItemCategory::where('category_id', $this->id)->where('main_menu_id', $main_menu)->pluck('item_id')->toArray();
        $items = Item::whereIn('id', $categoryItems)->get();
        return $items;
    }

    public function menus()
    {
        return $this->belongsToMany('App\Http\Models\Menu', 'category_menu', 'category_id', 'menu_id')->withTimestamps();
    }

    public function children()
    {
        return $this->hasMany('App\Http\Models\Category', 'parent_id');
    }

    public function posCategory()
    {
        return $this->hasMany('App\Http\Models\PosCategory', 'category_id');
    }

    public function parent()
    {
        return $this->belongsTo('App\Http\Models\Category', 'parent_id', 'id');
    }

    public function mainMenus()
    {
        return $this->belongsToMany('App\Http\Models\MainMenu', 'category_menu', 'category_id', 'main_menu_id')->withTimestamps();
    }
}
