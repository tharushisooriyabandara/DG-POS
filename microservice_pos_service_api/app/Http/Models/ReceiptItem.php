<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class ReceiptItem extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'receipt_item';
    protected $guarded = [];
}
