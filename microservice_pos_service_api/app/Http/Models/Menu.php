<?php
namespace App\Http\Models;

use App\Http\Models\Category;
use App\Http\Models\CategoryMenu;
use Illuminate\Database\Eloquent\Model;

class Menu extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'menu';
    protected $guarded = [];

    public function categories()
    {
        return $this->belongsToMany('App\Http\Models\Category', 'category_menu', 'menu_id', 'category_id')->orderBy('priority')->withTimestamps();
    }

    public function categoriesWithMainMenu()
    {
        return $this->belongsToMany('App\Http\Models\Category', 'category_menu', 'menu_id', 'category_id')->wherePivot('main_menu_id', $this->main_menu_id)->orderBy('priority')->withTimestamps();
    }

    public function categoryList($main_menu)
    {
        $categoryMenus = CategoryMenu::where('menu_id', $this->id)->where('main_menu_id', $main_menu)->pluck('category_id')->toArray();
        $categories = Category::whereIn('id', $categoryMenus)->orderBy('priority')->get();
        return $categories;
    }

    public function mainMenus()
    {
        return $this->belongsToMany('App\Http\Models\MainMenu', 'main_menu_menu', 'menu_id', 'main_menu_id')->withTimestamps();
    }

    public function mainMenu()
    {
        return $this->belongsTo('App\Http\Models\MainMenu', 'main_menu_id', 'id');
    }
}
