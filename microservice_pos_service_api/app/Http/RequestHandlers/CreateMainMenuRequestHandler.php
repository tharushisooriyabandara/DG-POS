<?php
namespace App\Http\RequestHandlers;

use Response;
use Request;
use App\Http\RequestHandlers\CommonRequest;

class CreateMainMenuRequestHandler extends CommonRequest
{
    public function authorize()
    {
        return true;
    }

    
    public function rules()
    {
        return [
            'name'         => 'required',
            'master_outlet'=> 'required'
        ];
    }

    public function messages()
    {
        $messages = [
            'name.required'        => 'Please enter the menu name.',
            'description.required'  => 'Please enter the description.',
            'status.required'       => 'Please select the status.'
        ];

        return $messages;
    }
}
