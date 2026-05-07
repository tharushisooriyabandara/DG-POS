<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class Receipt extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'receipts';
    protected $guarded = [];

    public function receiptItems()
    {
        return $this->hasMany('App\Http\Models\ReceiptItem', 'receipt_id');
    }
}
