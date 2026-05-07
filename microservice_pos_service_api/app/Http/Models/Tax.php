<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class Tax extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'taxes';
    protected $guarded = [];
}
