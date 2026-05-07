<?php
namespace App\Http\RequestHandlers;

use Response;
use Request;
use App\Http\RequestHandlers\CommonRequest;

class CreateItemRequestHandler extends CommonRequest
{
    public function authorize()
    {
        return true;
    }

    
    public function rules()
    {
        return [
            'title'         => 'required',
            'description'   => 'required',
            'status'        => 'required',
        ];
    }

    public function messages()
    {
        $messages = [
            'title.required'        => 'Please enter the item title.',
            'description.required'  => 'Please enter the description.',
            'price.required'        => 'Please enter the price.',
            'status.required'       => 'Please select the status.'
        ];

        return $messages;
    }
}
