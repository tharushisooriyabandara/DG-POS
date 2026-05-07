<?php

namespace App\Http\Controllers;

use Illuminate\Support\Facades\DB;
use App\Http\RequestHandlers\CreateItemRequestHandler;
use App\Http\RequestHandlers\UpdateItemRequestHandler;
use App\Http\RequestHandlers\SnoozeItemRequestHandler;
use App\Http\Services\ItemService;
use App\microservice_delivergate_api\Services\RequestHandleService;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Artisan;

class ItemController extends Controller
{
    private $item_service;

    public function __construct()
    {
        $this->item_service = new ItemService;
    }
    /**
     * Display a listing of the resource.
     *
     * @return \Illuminate\Http\Response
     */
    public function index(Request $request)
    {
        $result = $this->item_service->getItems(($request->has('main_menu') ? $request->get('main_menu') : null), ($request->has('item_id') ? $request->get('item_id') : null));
        return $result;
    }

    /**
     * Store a newly created resource in storage.
     *
     * @param  \Illuminate\Http\Request  $request
     * @return \Illuminate\Http\Response
     */
    public function store(CreateItemRequestHandler $request)
    {
        $result = $this->item_service->store($request->all());
        return $result;
    }

    /**
     * Display the specified resource.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function show($id, Request $request)
    {
        $result = $this->item_service->show($id, ($request->has('main_menu') ? $request->get('main_menu') : null));
        return $result;
    }

    public function getRemoteItem($platform, $id)
    {
        $result = $this->item_service->getRemoteItem($platform, $id);
        return $result;
    }

    /**
     * Display the items belongs to the order.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function itemCategories($id)
    {
        $result = $this->item_service->itemCategories($id);
        return $result;
    }

    /**
     * Update the specified resource in storage.
     *
     * @param  \Illuminate\Http\Request  $request
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function update(UpdateItemRequestHandler $request, $id)
    {
        $result = $this->item_service->update($request->all(), $id);
        return $result;
    }

    /**
     * Remove the specified resource from storage.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function destroy($id, Request $request)
    {
        $result = $this->item_service->destroy($id, ($request->has('main_menu') ? $request->get('main_menu') : null));
        return $result;
    }

    public function deleteEntityItems($id)
    {
        $result = $this->item_service->deleteEntityItems($id);
        return $result;
    }

    public function deleteBulkItems(Request $request)
    {
        $result = $this->item_service->deleteBulkItems($request->all());
        return $result;
    }

    public function savePlatformItemMapping(Request $request)
    {
        $result = $this->item_service->savePlatformItemMapping($request->all());
        return $result;
    }

    public function runArtisan(Request $request)
    {
        $output = Artisan::call($request->command);
        return $output;
    }

    public function runMigrate()
    {
        $request_handle_service = new RequestHandleService;
        $request_handle_service->loggerInfo("Running migrate on pos service", $this, __FUNCTION__, __LINE__);
        $result = $request_handle_service->runMigrate();
        return $result;
    }

    public function runSeeder()
    {
        $request_handle_service = new RequestHandleService;
        $request_handle_service->loggerInfo("Running seeder on pos service", $this, __FUNCTION__, __LINE__);
        $result = $request_handle_service->runSeeder();
        return $result;
    }

    public function getMenuItemDetails($id, Request $request)
    {
        $result = $this->item_service->getMenuItemDetails($id, ($request->has('main_menu') ? $request->get('main_menu') : null));
        return $result;
    }

    public function getItemsByQuery(Request $request)
    {
        $result = $this->item_service->getItemsByQuery($request->all());
        return $result;
    }

    public function getPlatformItems()
    {
        $result = $this->item_service->getPlatformItems();
        return $result;
    }

    public function getUnmatchedItems(Request $request)
    {
        $result = $this->item_service->getUnmatchedItems(($request->has('shop_id') ? $request->get('shop_id') : null));
        return $result;
    }

    public function createItemForRemaining($id, Request $request)
    {
        $result = $this->item_service->createItemForRemaining($id, ($request->has('main_menu') ? $request->get('main_menu') : null));
        return $result;
    }

    public function snoozeItem($shopid, $id)
    {
        $result = $this->item_service->snoozeItem($id, $shopid);
        return $result;
    }

    public function snoozeItems($shopid, SnoozeItemRequestHandler $request)
    {
        $result = $this->item_service->snoozeItems($shopid, $request->all());
        return $result;
    }

    public function updateProductDiscountPrice(Request $request)
    {
        $result = $this->item_service->updateProductDiscountPrice($request->all());
        return $result;
    }

    public function updateItemPrice(Request $request, $id)
    {
        $result = $this->item_service->updateItemPrice($request->all(), $id);
        return $result;
    }

    public function addOrRemoveCustomerFavouriteItem(Request $request)
    {
        $result = $this->item_service->addOrRemoveCustomerFavouriteItem($request->all());
        return $result;
    }

    public function getCustomerFavouriteItems($menu_id, Request $request)
    {
        $result = $this->item_service->getCustomerFavouriteOrPurchasedItems($menu_id, ($request->has('dp') ? $request->get('dp') : null), ($request->has('active') ? ($request->get('active') == 1) : false), ($request->has('query') ? $request->get('query') : ''), ($request->has('outlet') ? $request->get('outlet') : null), 'FAVOURITE');
        return $result;
    }

    public function getCustomerPurchasedItems($menu_id, Request $request, $brand_id)
    {
        $outlet = DB::table('main_menu')->where('id', $menu_id)->get()->first();
        $dp  = DB::table('delivery_platform')->where('outlet_id', $outlet->master_outlet)->where('platform_id', 6)->where('webshop_brand_id', $brand_id)->get()->first();
        $result = $this->item_service->getCustomerFavouriteOrPurchasedItems($menu_id, ($request->has('dp') ? $request->get('dp') : $dp->id), ($request->has('active') ? ($request->get('active') == 1) : false), ($request->has('query') ? $request->get('query') : ''), ($request->has('outlet') ? $request->get('outlet') : null), 'PURCHASED');
        return $result;
    }

    public function getCustomersByFavouriteItem($item_id)
    {
        $result = $this->item_service->getCustomersByFavouriteItem($item_id);
        return $result;
    }

    public function getPlatformItem($itemId, $platformId, $mainMenuId)
    {
        $result = $this->item_service->getPlatformItem($itemId, $platformId, $mainMenuId);
        return $result;
    }

    public function deleteItemImage($id)
    {
        $result = $this->item_service->deleteItemImage($id);
        return $result;
    }
}
