<?php

namespace App\Http\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class PrinterGroup extends Model
{
    use HasFactory;

    protected $table = 'printer_group';
    protected $guarded = [];
}
