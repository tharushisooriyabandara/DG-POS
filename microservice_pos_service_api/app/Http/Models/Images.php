<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class Images extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'images';
    protected $guarded = [];
    protected $hidden = ['id', 'type', 'type_id', 'created_at', 'updated_at'];
}
