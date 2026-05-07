<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class Configuration extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'configuration';
    protected $guarded = [];
}
