<?php
namespace App\Http\RequestHandlers;

use Response;
use Request;
use App\Http\RequestHandlers\CommonRequest;

class SnoozeItemRequestHandler extends CommonRequest
{
    public function authorize()
    {
        return true;
    }

    
    public function rules()
    {
        return [
            'item_ids'         => 'required',
        ];
    }

    public function messages()
    {
        $messages = [
            'item_ids.required'        => 'Please enter the Item ID',
        ];

        return $messages;
    }
}
