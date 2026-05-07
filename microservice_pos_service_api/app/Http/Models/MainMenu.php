<?php
namespace App\Http\Models;

use App\Http\Models\Item;
use App\Http\Models\ShopMainMenu;
use Illuminate\Database\Eloquent\Model;
use App\microservice_delivergate_api\Models\Shop;

class MainMenu extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'main_menu';
    protected $guarded = [];

    public function menus()
    {
        return $this->belongsToMany('App\Http\Models\Menu', 'main_menu_menu', 'main_menu_id', 'menu_id')->withTimestamps();
    }

    public function ownMenus()
    {
        return $this->hasMany('App\Http\Models\Menu', 'main_menu_id');
    }

    public function categories()
    {
        return $this->belongsToMany('App\Http\Models\Category', 'category_menu', 'main_menu_id', 'category_id')->orderBy('priority')->withTimestamps();
    }

    public function shops()
    {
        return $this->belongsToMany('App\Http\Models\Shop', 'shop_main_menu', 'main_menu_id', 'shop_id')->withTimestamps();
    }

    public function items()
    {
        $itemIds = unserialize($this->item_ids);
        if (!$itemIds) {
            $itemIds = [];
        }
        return Item::whereIn('id', $itemIds)->get()->fresh('categories', 'entityDeliveryPlatform', 'prices');
    }

    public function masterOutlet()
    {
        return $this->belongsTo('App\microservice_delivergate_api\Models\Shop', 'master_outlet', 'id');
    }

    public function activeShops()
    {
        $shopIds = ShopMainMenu::where('main_menu_id', $this->id)->pluck('shop_id')->toArray();
        $shops = Shop::whereIn('id', $shopIds)->get();
        return $shops;
        //return $this->hasMany('App\Http\Models\Shop', 'last_updated_menu');
    }

    public function modifiers()
    {
        return $this->hasMany('App\Http\Models\ModifierGroup', 'main_menu_id');
    }
}
