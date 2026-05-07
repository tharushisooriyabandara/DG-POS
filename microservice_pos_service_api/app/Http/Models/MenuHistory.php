<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;
use App\microservice_delivergate_api\Models\Shop;

class MenuHistory extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'menu_history';
    protected $guarded = [];

    public function masterOutlet()
    {
        return $this->belongsTo('App\microservice_delivergate_api\Models\Shop', 'master_outlet');
    }

    public function user()
    {
        return $this->belongsTo('App\microservice_delivergate_api\Models\User', 'user_id');
    }

    public function outlets()
    {
        $outletIds = unserialize($this->sub_outlet);
        $shops = Shop::whereIn('id', $outletIds)->get();
        return $shops;
    }
}
