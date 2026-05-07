<?php
namespace App\Http\RequestHandlers;

use Response;
use Request;
use App\Http\RequestHandlers\CommonRequest;

class UpdateCategoryRequestHandler extends CommonRequest
{
    public function authorize()
    {
        return true;
    }

    
    public function rules()
    {
        return [
            'title'         => 'required',
            'status'        => 'required',
        ];
    }

    public function messages()
    {
        $messages = [
            'title.required'        => 'Please enter the category title.',
            'description.required'  => 'Please enter the description.',
            'status.required'       => 'Please select the status.'
        ];

        return $messages;
    }
}
