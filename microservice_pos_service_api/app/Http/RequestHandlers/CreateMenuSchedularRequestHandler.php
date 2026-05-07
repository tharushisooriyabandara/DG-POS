<?php
namespace App\Http\RequestHandlers;

use Response;
use Request;
use App\Http\RequestHandlers\CommonRequest;

class CreateMenuSchedularRequestHandler extends CommonRequest
{
    public function authorize()
    {
        return true;
    }

    
    public function rules()
    {
        return [
            'main_menu_id'         => 'required',
            'publishable_date'=> 'required',
            'platform_ids'=> 'required',
            'status'=> 'required'
        ];
    }

    public function messages()
    {
        $messages = [
            'main_menu_id.required'        => 'Please select the main menu.',
            'publishable_date.required'  => 'Please enter the publishable date.',
            'status.required'       => 'Please select the status.'
        ];

        return $messages;
    }
}
