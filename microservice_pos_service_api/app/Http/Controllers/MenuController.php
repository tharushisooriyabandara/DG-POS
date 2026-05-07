<?php

namespace App\Http\Controllers;

use App\Http\Models\Menu;
use Illuminate\Support\Facades\DB;
use App\Http\RequestHandlers\CreateMenuRequestHandler;
use App\Http\RequestHandlers\UpdateMenuRequestHandler;
use App\Http\RequestHandlers\CreateMainMenuRequestHandler;
use App\Http\RequestHandlers\CreateMenuSchedularRequestHandler;
use App\Http\Services\MenuService;
use Illuminate\Http\Request;
use App\Http\Helpers\CommonHelper;

class MenuController extends Controller
{
    private $menu_service;

    public function __construct()
    {
        $this->menu_service = new MenuService;
    }
    /**
     * Display a listing of the resource.
     *
     * @return \Illuminate\Http\Response
     */
    public function index(Request $request)
    {
        if ($request->has('main_menu_id')) {
            $menu = Menu::where('main_menu_id', $request->get('main_menu_id'))->get()->fresh('categories', 'mainMenus');
        } else {
            $menu = Menu::all()->fresh('categories', 'mainMenus');
        }
        return json_encode(['status' => 200, 'data' => $menu]);
    }

    /**
     * Store a newly created resource in storage.
     *
     * @param  \Illuminate\Http\Request  $request
     * @return \Illuminate\Http\Response
     */
    public function store(CreateMenuRequestHandler $request)
    {
        $result = $this->menu_service->store($request->all());
        return $result;
    }

    /**
     * Display the specified resource.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function show($id)
    {
        $result = $this->menu_service->show($id);
        return $result;
    }

    /**
     * Display the items belongs to the order.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function menuCategories($id, Request $request)
    {
        $result = $this->menu_service->menuCategories($id, ($request->has('main_menu') ? $request->get('main_menu') : null));
        return $result;
    }

    public function menuItems($id, Request $request)
    {
        $result = $this->menu_service->menuItems($id, ($request->has('main_menu') ? $request->get('main_menu') : null));
        return $result;
    }

    /**
     * Update the specified resource in storage.
     *
     * @param  \Illuminate\Http\Request  $request
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function update(UpdateMenuRequestHandler $request, $id)
    {
        $result = $this->menu_service->update($request->all(), $id);
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
        $result = $this->menu_service->destroy($id, ($request->has('main_menu') ? $request->get('main_menu') : null));
        return $result;
    }

    public function uploadMenu()
    {
        $result = $this->menu_service->uploadRemoteMenu();
        return $result;
    }

    public function fetchMenu(Request $request)
    {
        $result = $this->menu_service->fetchMenu($request->all());
        return $result;
    }

    public function getPosMenu(Request $request)
    {
        $result = $this->menu_service->getPosMenu($request->all());
        return $result;
    }

    public function getWizardItems(Request $request)
    {
        $result = $this->menu_service->getWizardItems($request->all());
        return $result;
    }

    public function getWizardCategories(Request $request)
    {
        $result = $this->menu_service->getWizardCategories($request->all());
        return $result;
    }

    public function getWizardModifiers(Request $request)
    {
        $result = $this->menu_service->getWizardModifiers($request->all());
        return $result;
    }

    public function getWizardAllergies(Request $request)
    {
        $result = $this->menu_service->getWizardAllergies($request->all());
        return $result;
    }

    public function getWizardPlatforms(Request $request)
    {
        $result = $this->menu_service->getWizardPlatforms($request->all());
        return $result;
    }

    public function getMappingMenuList(Request $request)
    {
        $result = $this->menu_service->getMappingMenuList($request->all());
        return $result;
    }

    public function filterMappedItems(Request $request)
    {
        $result = $this->menu_service->filterMappedItems($request->all());
        return $result;
    }

    public function getMainMenu(Request $request)
    {
        $result = $this->menu_service->getMainMenu(($request->has('b') ? $request->get('b') : null));
        return $result;
    }

    public function storeMainMenu(CreateMainMenuRequestHandler $request)
    {
        $result = $this->menu_service->storeMainMenu($request->all());
        return $result;
    }

    public function showMainMenu($id, Request $request)
    {
        $result = $this->menu_service->showMainMenu($id, ($request->has('b') ? $request->get('b') : null));
        return $result;
    }

    public function showMainMenuMenus($id)
    {
        $result = $this->menu_service->showMainMenuMenus($id);
        return $result;
    }

    public function updateMainMenu(Request $request, $id)
    {
        $result = $this->menu_service->updateMainMenu($request->all(), $id);
        return $result;
    }

    public function updateMainMenuItems(Request $request, $id)
    {
        $result = $this->menu_service->updateMainMenuItems($request->all(), $id);
        return $result;
    }

    public function updateMainMenuPlatforms(Request $request, $id)
    {
        $result = $this->menu_service->updateMainMenuPlatforms($request->all(), $id);
        return $result;
    }

    public function destroyMainMenu($id)
    {
        $result = $this->menu_service->destroyMainMenu($id);
        return $result;
    }

    public function getMainMenuOutlets($id, Request $request)
    {
        $result = $this->menu_service->getMainMenuOutlets($id, ($request->has('b') ? $request->get('b') : null));
        return $result;
    }

    public function cloneMainMenu($id, Request $request)
    {
        $result = $this->menu_service->cloneMainMenu($id, $request->all());
        return $result;
    }

    public function getPreviewCategories($id, Request $request)
    {
        $result = $this->menu_service->getPreviewCategories($id, ($request->has('dp') ? $request->get('dp') : null), ($request->has('active') ? ($request->get('active') == 1) : false));
        return $result;
    }

    public function getPreviewItems($id, Request $request)
    {
        $result = $this->menu_service->getPreviewItems($id, ($request->has('dp') ? $request->get('dp') : null), ($request->has('active') ? ($request->get('active') == 1) : false), '', ($request->has('outlet_id') ? $request->get('outlet_id') : null));
        return $result;
    }

    public function getCategoriesWhichHasItems($id)
    {
        $result = $this->menu_service->getCategoriesWhichHasItems($id);
        return $result;
    }

    public function updateMainMenuStatus()
    {
        $result = $this->menu_service->updateMainMenuStatus();
        return $result;
    }

    public function searchInMainMenu($id, Request $request)
    {
        $result = $this->menu_service->searchInMainMenu($id, ($request->has('q') ? $request->get('q') : ''), ($request->has('outlet_id') ? $request->get('outlet_id') : ''), ($request->has('dp') ? ($request->get('dp') != 'null' ? $request->get('dp') : null) : null));
        return $result;
    }

    public function deleteOutletFromMainMenu($id, $outlet)
    {
        $result = $this->menu_service->deleteOutletFromMainMenu($id, $outlet);
        return $result;
    }

    public function getEntityItems($id)
    {
        $result = $this->menu_service->getEntityItems($id);
        return $result;
    }

    public function updateStatus($id, Request $request)
    {
        $result = $this->menu_service->updateStatus($id, $request->get('status'));
        return $result;
    }
    public function getProductByShopidMenu($shopId, $menueId, Request $request)
    {
        $result = $this->menu_service->getProductByShopidMenu($shopId, $menueId, ($request->has('dp') ? $request->get('dp') : null), );
        return $result;
    }
    public function getCategoryByShopId($brandId, $shopId)
    {
        $result = $this->menu_service->getCategoryByShopId($brandId, $shopId, 'WEBSHOP');
        return $result;
    }
    public function getWebshopCategoryItemsByMenuIdAndShopId($id, Request $request, $brandId, $shopId)
    {
        $dp  = DB::table('delivery_platform')->where('outlet_id', $shopId)->where('platform_id', 6)->where('webshop_brand_id', $brandId)->get()->first();
        if (is_null($dp)) {
            \Log::error('Delivery platform not found to load the data. - '.CommonHelper::getXTenantCode($_SERVER));
            return json_encode(['status' => 404, 'data' => []]);
        }
        $result = $this->menu_service->getWebshopCategoryItemsByMenuIdAndShopId($id, $dp->id, ($request->has('active') ? ($request->get('active') == 1) : true), '', $shopId);
        return $result;
    }

    public function getWebshopItemsByMenuIdAndShopId($id, Request $request, $brandId, $shopId)
    {
        $dp  = DB::table('delivery_platform')->where('outlet_id', $shopId)->where('platform_id', 6)->where('webshop_brand_id', $brandId)->get()->first();
        if (is_null($dp)) {
            \Log::error('Delivery platform not found to load the data. - '.CommonHelper::getXTenantCode($_SERVER));
            return json_encode(['status' => 404, 'data' => []]);
        }
        $result = $this->menu_service->getWebshopItemsByMenuIdAndShopId($id, $dp->id, ($request->has('active') ? ($request->get('active') == 1) : true), ($request->has('q') ? $request->get('q') : ''), $shopId);
        return $result;
    }

    public function updateWebshopMenu($id, Request $request, $brandId, $shopId)
    {
        $dp  = DB::table('delivery_platform')->where('outlet_id', $shopId)->where('platform_id', 6)->where('webshop_brand_id', $brandId)->get()->first();
        if (is_null($dp)) {
            \Log::error('Delivery platform not found to load the data. - '.CommonHelper::getXTenantCode($_SERVER));
            return json_encode(['status' => 404, 'data' => []]);
        }
        $result = $this->menu_service->updateWebshopMenu($id, $dp->id, ($request->has('active') ? ($request->get('active') == 1) : true), '', $shopId, ($request->has('batch') ? $request->get('batch'):null));
        return $result;
    }

    public function filterCategoryItems($id, Request $request, $category, $brandId, $shopId)
    {
        $dp  = DB::table('delivery_platform')->where('outlet_id', $shopId)->where('platform_id', 6)->where('webshop_brand_id', $brandId)->get()->first();
        if (is_null($dp)) {
            \Log::error('Delivery platform not found to load the data. - '.CommonHelper::getXTenantCode($_SERVER));
            return json_encode(['status' => 404, 'data' => []]);
        }
        $result = $this->menu_service->filterCategoryItems($id, $dp->id, ($request->has('active') ? ($request->get('active') == 1) : true), '', $category, $shopId);
        return $result;
    }

    /*Main menu schedular*/
    public function getScheduledMainMenus()
    {
        $result = $this->menu_service->getScheduledMainMenus();
        return $result;
    }

    public function createScheduledMainMenu(CreateMenuSchedularRequestHandler $request)
    {
        $result = $this->menu_service->createScheduledMainMenu($request->all());
        return $result;
    }

    public function showScheduledMainMenu($id)
    {
        $result = $this->menu_service->showScheduledMainMenu($id);
        return $result;
    }

    public function updateScheduledMainMenu(CreateMenuSchedularRequestHandler $request, $id)
    {
        $result = $this->menu_service->updateScheduledMainMenu($request->all(), $id);
        return $result;
    }

    public function destroyScheduledMainMenu($id)
    {
        $result = $this->menu_service->destroyScheduledMainMenu($id);
        return $result;
    }

    public function getMainMenuByBrandAndShop($brandId, $shopId)
    {
        $result = $this->menu_service->getMainMenuByBrandAndShop($brandId, $shopId);
        return $result;
    }

    public function updateTableOrderMenu($id, Request $request, $brandId, $shopId)
    {
        $dp  = DB::table('delivery_platform')->where('outlet_id', $shopId)->where('platform_id', 8)->where('webshop_brand_id', $brandId)->get()->first();
        if (is_null($dp)) {
            \Log::error('Delivery platform not found to load the data. - '.CommonHelper::getXTenantCode($_SERVER));
            return json_encode(['status' => 404, 'data' => []]);
        }
        $result = $this->menu_service->updateWebshopMenu($id, $dp->id, ($request->has('active') ? ($request->get('active') == 1) : true), '', $shopId, ($request->has('batch') ? $request->get('batch'):null));
        return $result;
    }

    public function getTableOrderCategoryByShopId($brandId, $shopId)
    {
        $result = $this->menu_service->getCategoryByShopId($brandId, $shopId, 'TABLE_ORDER');
        return $result;
    }

    public function getTableOrderCategoryItemsByMenuIdAndShopId($id, Request $request, $brandId, $shopId)
    {
        $dp  = DB::table('delivery_platform')->where('outlet_id', $shopId)->where('platform_id', 8)->where('webshop_brand_id', $brandId)->get()->first();
        if (is_null($dp)) {
            \Log::error('Delivery platform not found to load the data. - '.CommonHelper::getXTenantCode($_SERVER));
            return json_encode(['status' => 404, 'data' => []]);
        }
        $result = $this->menu_service->getWebshopCategoryItemsByMenuIdAndShopId($id, $dp->id, ($request->has('active') ? ($request->get('active') == 1) : true), '', $shopId);
        return $result;
    }

    public function filterTableOrderCategoryItems($id, Request $request, $category, $brandId, $shopId)
    {
        $dp  = DB::table('delivery_platform')->where('outlet_id', $shopId)->where('platform_id', 8)->where('webshop_brand_id', $brandId)->get()->first();
        if (is_null($dp)) {
            \Log::error('Delivery platform not found to load the data. - '.CommonHelper::getXTenantCode($_SERVER));
            return json_encode(['status' => 404, 'data' => []]);
        }
        $result = $this->menu_service->filterCategoryItems($id, $dp->id, ($request->has('active') ? ($request->get('active') == 1) : true), '', $category, $shopId);
        return $result;
    }

    public function getSnoozeItemList($id, Request $request)
    {
        $result = $this->menu_service->getSnoozeItemList($id, ($request->has('q') ? $request->get('q') : ''));
        return $result;
    }

    public function getWebshopMenuTimes($id, $brandId, $shopId)
    {
        $dp  = DB::table('delivery_platform')->where('outlet_id', $shopId)->where('platform_id', 6)->where('webshop_brand_id', $brandId)->get()->first();
        if (is_null($dp)) {
            \Log::error('Delivery platform not found to load the data. - '.CommonHelper::getXTenantCode($_SERVER));
            return json_encode(['status' => 404, 'data' => []]);
        }
        $result = $this->menu_service->getWebshopMenuTimes($id, $dp->id, $shopId);
        return $result;
    }

    public function getOptimizedSnoozeItemList($id, Request $request)
    {
        $result = $this->menu_service->getOptimizedSnoozeItemList($id, $request->all());
        return $result;
    }

    public function updateSnoozeItemListJson($id)
    {
        $result = $this->menu_service->updateSnoozeItemListJson($id);
        return $result;
    }

    public function updateDgPosMenu($id, Request $request, $brandId, $shopId)
    {
        $dp  = DB::table('delivery_platform')->where('outlet_id', $shopId)->where('platform_id', 9)->where('webshop_brand_id', $brandId)->get()->first();
        if (is_null($dp)) {
            \Log::error('Delivery platform not found to load the data. - '.CommonHelper::getXTenantCode($_SERVER));
            return json_encode(['status' => 404, 'data' => []]);
        }
        $result = $this->menu_service->updateWebshopMenu($id, $dp->id, ($request->has('active') ? ($request->get('active') == 1) : true), '', $shopId, ($request->has('batch') ? $request->get('batch'):null));
        return $result;
    }
}
