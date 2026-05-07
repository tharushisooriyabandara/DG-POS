<?php

namespace App\Http\Controllers;

use Illuminate\Http\Request;
use App\Http\Services\PosService;
use App\Http\Services\Pos\EposService;
use App\Http\RequestHandlers\CreatePOSRequestHandler;
use App\Http\RequestHandlers\LocationUpdateRequestHandler;

class PosController extends Controller
{
    private $pos_service;

    public function __construct()
    {
        $this->pos_service = new PosService;
    }

    public function index(Request $request)
    {
        $franchise = ($request->has('franchise') ? ($request->get('franchise') != 'null' ? $request->get('franchise') : null) : null);
        $shop = ($request->has('shop') ? ($request->get('shop') != 'null' ? $request->get('shop') : null) : null);
        $result = $this->pos_service->index($franchise, $shop);
        return $result;
    }

    public function getPosPagination(Request $request)
    {
        $page = ($request->has('page') ? $request->get('page') : null);
        $franchise = ($request->has('franchise') ? ($request->get('franchise') != 'null' ? $request->get('franchise') : null) : null);
        $shop = ($request->has('shop') ? ($request->get('shop') != 'null' ? $request->get('shop') : null) : null);
        $query = ($request->has('q') ? $request->get('q'): null);
        $result = $this->pos_service->getPosPagination($franchise, $shop, $page, $query);
        return $result;
    }

    public function create(CreatePOSRequestHandler $request)
    {
        $result = $this->pos_service->create($request->all());
        return $result;
    }

    public function show($id)
    {
        $result = $this->pos_service->show($id);
        return $result;
    }

    public function update(CreatePOSRequestHandler $request, $id)
    {
        $result = $this->pos_service->update($request->all(), $id);
        return $result;
    }

    public function processOrderRequest(Request $request)
    {
        $result = $this->pos_service->getApproval($request->all());
        return json_encode(['status' => 200, 'response' => $result]);
    }

    public function setRestaurantStatus(Request $request)
    {
        $result = $this->pos_service->setRestaurantStatus($request->all());
        return $result;
    }

    public function getRestaurantStatus($id)
    {
        $result = $this->pos_service->getRestaurantStatus($id);
        return $result;
    }

    // Not using currently can remove later
    // public function setAutoAcceptStatus(Request $request)
    // {
    //     $result = $this->pos_service->setAutoAcceptStatus($request->all());
    //     return $result;
    // }

    // public function getAutoAcceptStatus()
    // {
    //     $result = $this->pos_service->getAutoAcceptStatus();
    //     return $result;
    // }

    public function getPosCategories(Request $request)
    {
        $result = $this->pos_service->getPosCategories($request->all());
        return $result;
    }

    public function getPosCategoryById(Request $request, $id)
    {
        $result = $this->pos_service->getPosCategoryById($request->all(), $id);
        return $result;
    }

    public function fetchPosCategories(Request $request)
    {
        $result = $this->pos_service->fetchPosCategories($request->all());
        return $result;
    }

    public function getPosItems(Request $request)
    {
        $result = $this->pos_service->getPosItems($request->all());
        return $result;
    }

    public function getPosItemById(Request $request, $id)
    {
        $result = $this->pos_service->getPosItemById($request->all(), $id);
        return $result;
    }

    public function fetchPosItems(Request $request)
    {
        $result = $this->pos_service->fetchPosItems($request->all());
        return $result;
    }

    public function getTenderTypes(Request $request)
    {
        $result = $this->pos_service->getTenderTypes($request->all());
        return $result;
    }

    public function syncWithPos(Request $request)
    {
        $result = $this->pos_service->syncWithPos(($request->has('shop_id') ? $request->get('shop_id') : null), ($request->has('update_modifiers') ? ($request->get('update_modifiers')=='true') : true));
        return $result;
    }

    public function createTransactions(Request $request)
    {
        $eposService = new EposService;
        $result = $eposService->createTransactions($request->all());
        return $result;
    }

    public function getTransactions(Request $request)
    {
        $eposService = new EposService;
        $result = $eposService->getTransactions($request->get('shop_id'));
        return $result;
    }

    public function getSingleTransactions($id, Request $request)
    {
        $eposService = new EposService;
        $result = $eposService->getSingleTransactions($id, ($request->has('type') ? $request->get('type') : null), ($request->has('shop_id') ? $request->get('shop_id') : null));
        return $result;
    }

    public function deleteTransaction($id, Request $request)
    {
        $eposService = new EposService;
        $result = $eposService->deleteTransaction($id, ($request->has('shop_id') ? $request->get('shop_id') : null));
        return $result;
    }

    public function getPosModifiers(Request $request)
    {
        $result = $this->pos_service->getPosModifiers($request->all());
        return $result;
    }

    public function getPosModifierById(Request $request, $id)
    {
        $result = $this->pos_service->getPosModifierById($request->all(), $id);
        return $result;
    }

    public function fetchPosModifiers(Request $request)
    {
        $result = $this->pos_service->fetchPosModifiers($request->all());
        return $result;
    }

    public function getPosTaxes(Request $request)
    {
        $result = $this->pos_service->getPosTaxes($request->all());
        return $result;
    }

    // Need to remove if not needed
    // public function getPosTaxById(Request $request, $id)
    // {
    //     $result = $this->pos_service->getPosTaxById($request->all(), $id);
    //     return $result;
    // }

    public function fetchPosTaxes(Request $request)
    {
        $result = $this->pos_service->fetchPosTaxes($request->all());
        return $result;
    }

    public function createReceipt(Request $request)
    {
        $result = $this->pos_service->createReceipt($request->all());
        return $result;
    }

    public function createRefund(Request $request)
    {
        $result = $this->pos_service->createRefund($request->all());
        return $result;
    }

    public function getPaymentsTypes(Request $request)
    {
        $result = $this->pos_service->getPaymentsTypes($request->all());
        return $result;
    }

    public function fetchPaymentsTypes(Request $request)
    {
        $result = $this->pos_service->fetchPaymentsTypes($request->all());
        return $result;
    }

    public function updateTransactionsById(Request $request, $id)
    {
        $eposService = new EposService;
        $result = $eposService->updateTransactionsById($id, $request->all());
        return $result;
    }

    public function fetchAll(Request $request)
    {
        $result = $this->pos_service->fetchAll($request->all());
        return $result;
    }

    public function getStoreDetails($id)
    {
        $result = $this->pos_service->getStoreDetails($id);
        return $result;
    }

    public function updateStoreDetails(Request $request)
    {
        $result = $this->pos_service->updateStoreDetails($request->all());
        return $result;
    }

    public function posTypes()
    {
        $result = $this->pos_service->getPosTypes();
        return $result;
    }

    public function delete($id)
    {
        $result = $this->pos_service->delete($id);
        return $result;
    }

    public function getActiveShopMenu($id, Request $request)
    {
        $result = $this->pos_service->getActiveShopMenu($id, ($request->has('q') ? $request->get('q') : ''));
        return $result;
    }

    public function updateServiceAvailability(Request $request)
    {
        $result = $this->pos_service->updateServiceAvailability($request->all());
        return $result;
    }
    public function locationUpdate(Request $request)
    {
        $result = $this->pos_service->locationUpdate($request->all());
        return $result;
    }
}
