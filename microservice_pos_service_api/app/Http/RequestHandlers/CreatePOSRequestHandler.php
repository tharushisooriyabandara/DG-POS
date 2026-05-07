<?php
namespace App\Http\RequestHandlers;

use App\Http\RequestHandlers\CommonRequest;

class CreatePOSRequestHandler extends CommonRequest
{
    public function authorize()
    {
        return true;
    }

    public function rules()
    {
        return [
            'shop_id' => 'required',
            'type' => 'required',
            'parameters' => 'required',
            'status' => 'required',
            'platform_id' => 'required',
            'franchise_id' => 'required',
        ];
    }
}
