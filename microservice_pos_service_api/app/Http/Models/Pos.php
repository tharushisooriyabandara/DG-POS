<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class Pos extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'pos';
    protected $guarded = [];

    protected $hidden = [
        'parameters',
        'parameter_values'
    ];

    public function outlet()
    {
        return $this->belongsTo('App\microservice_delivergate_api\Models\Shop','shop_id');
    }
}
