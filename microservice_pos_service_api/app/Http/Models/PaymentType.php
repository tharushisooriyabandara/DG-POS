<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class PaymentType extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'payment_types';
    protected $guarded = [];
}
