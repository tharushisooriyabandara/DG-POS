<?php
namespace App\Http\RequestHandlers;

use App\Http\RequestHandlers\CommonRequest;

class BaseRequest extends CommonRequest
{
    public function authorize()
    {
        return true;
    }

    
    public function rules()
    {
        return [
        ];
    }
}
