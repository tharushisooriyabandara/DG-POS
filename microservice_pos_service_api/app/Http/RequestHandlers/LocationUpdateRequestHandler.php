<?php
namespace App\Http\RequestHandlers;

use Response;
use Request;
use App\Http\RequestHandlers\CommonRequest;

class LocationUpdateRequestHandler extends CommonRequest
{
    public function authorize()
    {
        return true;
    }

    
    public function rules()
    {
        return [
            'latitude'  => 'required',
            'longitude' => 'required',
            'country_Code'=> 'required',
        ];
    }

    public function messages()
    {
        $messages = [
            
        ];

        return $messages;
    }
}
