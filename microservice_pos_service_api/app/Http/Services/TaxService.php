<?php

namespace App\Http\Services;

use App\Http\Models\Tax;
use App\microservice_delivergate_api\Services\BaseService as BaseService;
use Exception;

class TaxService extends BaseService
{
    public function store($data)
    {
        try {
            $tax = new Tax;
            $tax->id = $data['id'];
            if (isset($data['shop_id'])) {
                $tax->shop_id = $data['shop_id'];
            }
            $tax->type = $data['type'];
            $tax->name = $data['name'];
            $tax->rate = $data['rate'];
            $tax->status = $data['status'];
            $tax->save();
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
        return $this->success('Successfully created the Tax.');
    }

    public function show($id)
    {

    }

    public function update($data, $id)
    {
        try {
            $tax = Tax::find($id);
            if (isset($data['shop_id'])) {
                $tax->shop_id = $data['shop_id'];
            }
            $tax->type = $data['type'];
            $tax->name = $data['name'];
            $tax->rate = $data['rate'];
            $tax->status = $data['status'];
            $tax->save();

        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
        return $this->success('Successfully updated the Tax.');
    }

    public function destroy($id)
    {

    }
}
