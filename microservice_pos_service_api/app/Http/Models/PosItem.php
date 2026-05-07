<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class PosItem extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'pos_items';
    protected $guarded = [];
}
