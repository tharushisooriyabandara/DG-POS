<?php

namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class MainMenuSchedular extends Model
{
    protected $table = 'main_menu_schedular';
    protected $guarded = [];

    public function mainMenu()
    {
        return $this->belongsTo('App\Http\Models\MainMenu', 'main_menu_id', 'id');
    }
}
