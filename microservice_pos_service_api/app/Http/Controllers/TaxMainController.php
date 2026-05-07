<?php

namespace App\Http\Controllers;

use Illuminate\Http\Request;
use App\Http\Services\TaxMainService;

class TaxMainController extends Controller
{
    private $tax_main_service;

    public function __construct()
    {
        $this->tax_main_service = new TaxMainService();
    }

    public function getTaxes(Request $request)
    {
        $result = $this->tax_main_service->getTaxes(($request->has('active') ? ($request->get('active') != '' ? $request->get('active') : null) : null), ($request->has('q') ? ($request->get('q') != '' ? $request->get('q') : null) : null));
        return $result;
    }

    public function storeTax(Request $request)
    {
        $result = $this->tax_main_service->storeTax($request->all());
        return $result;
    }

    public function getTax($id)
    {
        $result = $this->tax_main_service->getTax($id);
        return $result;
    }

    public function updateTax(Request $request, $id)
    {
        $result = $this->tax_main_service->updateTax($request->all(), $id);
        return $result;
    }

    public function destroyTax($id)
    {
        $result = $this->tax_main_service->destroyTax($id);
        return $result;
    }

    public function getTaxProfiles(Request $request)
    {
        $result = $this->tax_main_service->getTaxProfiles(($request->has('active') ? ($request->get('active') != '' ? $request->get('active') : null) : null), ($request->has('q') ? ($request->get('q') != '' ? $request->get('q') : null) : null));
        return $result;
    }

    public function storeTaxProfile(Request $request)
    {
        $result = $this->tax_main_service->storeTaxProfile($request->all());
        return $result;
    }

    public function getTaxProfile($id)
    {
        $result = $this->tax_main_service->getTaxProfile($id);
        return $result;
    }

    public function updateTaxProfile(Request $request, $id)
    {
        $result = $this->tax_main_service->updateTaxProfile($request->all(), $id);
        return $result;
    }

    public function destroyTaxProfile($id)
    {
        $result = $this->tax_main_service->destroyTaxProfile($id);
        return $result;
    }

    public function getTaxConditionTypes()
    {
        $result = $this->tax_main_service->getTaxConditionTypes();
        return $result;
    }
}
