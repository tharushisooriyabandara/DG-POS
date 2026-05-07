<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class PosType extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'pos_types';
    protected $guarded = [];

    protected $hidden = [
        'parameters',
        'parameter_values'
    ];
}
