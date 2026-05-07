<?php
namespace App\Http\RequestHandlers;

use Response;
use Request;
use App\Http\RequestHandlers\CommonRequest;

class CreateMenuRequestHandler extends CommonRequest
{
    public function authorize()
    {
        return true;
    }

    
    public function rules()
    {
        return [
            'main_menu'     => 'required',
            'title'         => 'required',
            'description'   => 'required',
            'status'        => 'required',
        ];
    }

    public function messages()
    {
        $messages = [
            'title.required'        => 'Please enter the menu title.',
            'description.required'  => 'Please enter the description.',
            'status.required'       => 'Please select the status.'
        ];

        return $messages;
    }
}
