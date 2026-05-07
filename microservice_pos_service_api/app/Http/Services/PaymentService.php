<?php

namespace App\Http\Services;

use App\Http\Models\PaymentType;
use App\microservice_delivergate_api\Services\BaseService as BaseService;
use Exception;

class PaymentService extends BaseService
{
    public function store($data)
    {
        try {
            $payment = new PaymentType;
            $payment->uuid = $data['uuid'];
            if (isset($data['shop_id'])) {
                $payment->shop_id = $data['shop_id'];
            }
            $payment->type = $data['type'];
            $payment->name = $data['name'];
            $payment->status = $data['status'];
            $payment->save();
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
        return $this->success('Successfully created the Payment types.');
    }

    public function show($id)
    {

    }

    public function update($data, $id)
    {
        try {
            $payment = PaymentType::where('uuid', $id)->where('shop_id', $data['shop_id'])->first();
            $payment->type = $data['type'];
            $payment->name = $data['name'];
            $payment->status = $data['status'];
            $payment->save();
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
        return $this->success('Successfully updated the Payment types.');
    }

    public function destroy($id)
    {

    }
}
