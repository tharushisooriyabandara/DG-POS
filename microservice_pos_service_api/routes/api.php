<?php

use Illuminate\Support\Facades\Route;

/*
|--------------------------------------------------------------------------
| API Routes
|--------------------------------------------------------------------------
|
| Here is where you can register API routes for your application. These
| routes are loaded by the RouteServiceProvider within a group which
| is assigned the "api" middleware group. Enjoy building your API!
|
 */
Route::group(['prefix' => 'v1'], function () {
    Route::get('/run_artisan', 'ItemController@runArtisan');
    Route::get('/run_migrate', 'ItemController@runMigrate');
    Route::get('/run_seeder', 'ItemController@runSeeder');

    Route::group(['middleware' => 'client'], function () {
        Route::post('process-order-request', 'PosController@processOrderRequest');
        Route::get('upload-menu', 'MenuController@uploadMenu');
        Route::post('fetch-menu', 'MenuController@fetchMenu');
        Route::get('get-pos-menu', 'MenuController@getPosMenu');
        Route::get('get-mapping-menu-list', 'MenuController@getMappingMenuList');
        Route::get('filter-mapped-items', 'MenuController@filterMappedItems');
        Route::get('get-wizard-items', 'MenuController@getWizardItems');
        Route::get('get-wizard-categories', 'MenuController@getWizardCategories');
        Route::get('get-wizard-modifiers', 'MenuController@getWizardModifiers');
        Route::get('get-wizard-allergies', 'MenuController@getWizardAllergies');
        Route::get('get-wizard-platforms', 'MenuController@getWizardPlatforms');
        Route::get('get-menu-item-details/{id}', 'ItemController@getMenuItemDetails');

        Route::get('get-unmatched-items', 'ItemController@getUnmatchedItems');
        Route::get('create-item-for-remaining/{id}', 'ItemController@createItemForRemaining');

        Route::group(['prefix' => 'items'], function () {
            Route::get('/', 'ItemController@index');
            Route::post('/', 'ItemController@store');
            Route::post('filter', 'ItemController@getItemsByQuery');
            Route::get('snooze/{shopid}/{id}', 'ItemController@snoozeItem');
            Route::post('snooze/{shopid}', 'ItemController@snoozeItems');
            Route::get('remote/{platform}/{id}', 'ItemController@getRemoteItem');
            Route::get('{id}', 'ItemController@show');
            Route::get('{itemId}/platform/{platformId}/main-menu/{mainMenuId}', 'ItemController@getPlatformItem');
            Route::get('{id}/categories', 'ItemController@itemCategories');
            Route::put('{id}', 'ItemController@update');
            Route::get('entity-items/{id}', 'ItemController@deleteEntityItems');
            Route::delete('{id}', 'ItemController@destroy');
            Route::delete('image/{id}', 'ItemController@deleteItemImage');
            Route::post('delete-bulk-items', 'ItemController@deleteBulkItems');
            Route::put('/update-price/{id}', 'ItemController@updateItemPrice');
        });
        Route::get('platform-items', 'ItemController@getPlatformItems');
        Route::put('save-platform-item-mapping', 'ItemController@savePlatformItemMapping');

        Route::group(['prefix' => 'categories'], function () {
            Route::get('/', 'CategoryController@index');
            Route::post('/', 'CategoryController@store');
            Route::post('update-priority', 'CategoryController@updateCategoryPriority');
            Route::get('{id}', 'CategoryController@show');
            Route::get('{id}/menus', 'CategoryController@categoryMenus');
            Route::get('{id}/items', 'CategoryController@categoryItems');
            Route::put('{id}', 'CategoryController@update');
            Route::delete('{id}', 'CategoryController@destroy');
            Route::get('shopid/{id}', 'MenuController@getCategoryByShopId');
        });

        Route::group(['prefix' => 'menus'], function () {
            Route::get('/', 'MenuController@index');
            Route::post('/', 'MenuController@store');
            Route::get('{id}', 'MenuController@show');
            Route::get('{id}/categories', 'MenuController@menuCategories');
            Route::get('{id}/items', 'MenuController@menuItems');
            Route::put('{id}', 'MenuController@update');
            Route::delete('{id}', 'MenuController@destroy');
        });

        Route::group(['prefix' => 'modifier-group'], function () {
            Route::get('/', 'ModifierController@index');
            Route::post('/', 'ModifierController@store');
            Route::get('{id}', 'ModifierController@show');
            Route::put('{id}', 'ModifierController@update');
            Route::get('{id}/items', 'ModifierController@modifierGroupItems');
            Route::get('{id}/modifier-items', 'ModifierController@modifierGroupModifierItems');
            Route::delete('{id}', 'ModifierController@destroy');
            Route::post('find', 'ModifierController@findModifierFromItemAndModifierItem');
        });

        Route::group(['prefix' => 'main-menu'], function () {
            Route::group(['prefix' => 'schedule'], function () {
                Route::get('/', 'MenuController@getScheduledMainMenus');
                Route::post('/', 'MenuController@createScheduledMainMenu');
                Route::get('{id}', 'MenuController@showScheduledMainMenu');
                Route::put('{id}', 'MenuController@updateScheduledMainMenu');
                Route::delete('{id}', 'MenuController@destroyScheduledMainMenu');
            });
            Route::put('update-status/{id}', 'MenuController@updateStatus');
            Route::get('/', 'MenuController@getMainMenu');
            Route::post('/', 'MenuController@storeMainMenu');
            Route::get('status-update', 'MenuController@updateMainMenuStatus');
            Route::get('{id}', 'MenuController@showMainMenu');
            Route::get('{id}/categories-with-items', 'MenuController@getCategoriesWhichHasItems');
            Route::get('{id}/categories', 'MenuController@getPreviewCategories');
            Route::get('{id}/items', 'MenuController@getPreviewItems');
            Route::get('{id}/entity-items', 'MenuController@getEntityItems');
            Route::get('{id}/search', 'MenuController@searchInMainMenu');
            Route::post('{id}/clone', 'MenuController@cloneMainMenu');
            Route::get('{id}/menus', 'MenuController@showMainMenuMenus');
            Route::get('{id}/outlets', 'MenuController@getMainMenuOutlets');
            Route::put('{id}', 'MenuController@updateMainMenu');
            Route::delete('{id}', 'MenuController@destroyMainMenu');
            Route::delete('{id}/outlets/{outlet}', 'MenuController@deleteOutletFromMainMenu');
            Route::put('{id}/items', 'MenuController@updateMainMenuItems');
            Route::put('{id}/platforms', 'MenuController@updateMainMenuPlatforms');
            Route::get('items/{shopId}/{menueId}', 'MenuController@getProductByShopidMenu');
            Route::get('webshop-brand/{brandId}/shop/{shopId}', 'MenuController@getMainMenuByBrandAndShop');
        });

        Route::group(['prefix' => 'store'], function () {
            Route::get('status/{id}', 'PosController@getRestaurantStatus');
            Route::post('status', 'PosController@setRestaurantStatus');
            Route::get('details/{id}', 'PosController@getStoreDetails');
            Route::put('details', 'PosController@updateStoreDetails');
        });

        Route::group(['prefix' => 'outlet'], function () {
            Route::get('{id}/main-menu', 'PosController@getActiveShopMenu');
            Route::get('{id}/snooze-item-list', 'MenuController@getSnoozeItemList');
            Route::get('{id}/optimized-snooze-item-list', 'MenuController@getOptimizedSnoozeItemList');
            Route::put('{id}/update-snooze-item-list', 'MenuController@updateSnoozeItemListJson');
            Route::post('availability', 'PosController@updateServiceAvailability');
            Route::put('update-location', 'PosController@locationUpdate');
        });

        Route::group(['prefix' => 'webshop'], function () {
            Route::put('main-menu/{id}/categories/webshop-brand/{brandId}/shop/{shopId}', 'MenuController@updateWebshopMenu');
        });

        Route::group(['prefix' => 'table-order'], function () {
            Route::put('main-menu/{id}/categories/webshop-brand/{brandId}/shop/{shopId}', 'MenuController@updateTableOrderMenu');
        });

        Route::group(['prefix' => 'dg-pos'], function () {
            Route::put('main-menu/{id}/categories/webshop-brand/{brandId}/shop/{shopId}', 'MenuController@updateDgPosMenu');
        });

        Route::group(['prefix' => 'customer'], function () {
            Route::post('fav-items', 'ItemController@addOrRemoveCustomerFavouriteItem');
            Route::get('main-menu/{menu_id}/fav-items', 'ItemController@getCustomerFavouriteItems');
            Route::get('main-menu/{menu_id}/webshop-brand/{brand_id}/purchased-items', 'ItemController@getCustomerPurchasedItems');
            Route::get('fav-item/{item_id}', 'ItemController@getCustomersByFavouriteItem');
        });

        /*Route::group(['prefix' => 'auto-accept'], function () {
        Route::get('status/{id}', 'PosController@getAutoAcceptStatus');
        Route::put('status', 'PosController@setAutoAcceptStatus');
        });*/

        // POS service endpoints
        Route::group(['prefix' => 'pos'], function () {
            Route::post('create-transaction', 'PosController@createTransactions');
            Route::put('update-transaction/{id}', 'PosController@updateTransactionsById');
            Route::get('transaction', 'PosController@getTransactions');
            Route::get('transaction/{id}', 'PosController@getSingleTransactions');
            Route::delete('transaction/{id}', 'PosController@deleteTransaction');

            Route::get('sync', 'PosController@syncWithPos');

            Route::get('get-tender-types', 'PosController@getTenderTypes');

            Route::group(['prefix' => 'categories'], function () {
                Route::get('fetch', 'PosController@fetchPosCategories');
                Route::get('/', 'PosController@getPosCategories');
                Route::get('{id}', 'PosController@getPosCategoryById');
            });

            Route::group(['prefix' => 'items'], function () {
                Route::get('fetch', 'PosController@fetchPosItems');
                Route::get('/', 'PosController@getPosItems');
                Route::get('{id}', 'PosController@getPosItemById');
            });

            Route::group(['prefix' => 'modifiers'], function () {
                Route::get('fetch', 'PosController@fetchPosModifiers');
                Route::get('/', 'PosController@getPosModifiers');
                Route::get('{id}', 'PosController@getPosModifierById');
            });
            Route::group(['prefix' => 'taxes'], function () {
                Route::get('fetch', 'PosController@fetchPosTaxes');
                Route::get('/', 'PosController@getPosTaxes');
                //  Route::get('{id}', 'PosController@getPosTaxById');
            });

            Route::group(['prefix' => 'receipt'], function () {
                Route::post('create', 'PosController@createReceipt');
                Route::post('refund', 'PosController@createRefund');
            });

            Route::group(['prefix' => 'payment'], function () {
                Route::get('get/types', 'PosController@getPaymentsTypes');
                Route::get('fetch/types', 'PosController@fetchPaymentsTypes');
            });

            Route::group(['prefix' => 'fetch'], function () {
                Route::get('all', 'PosController@fetchAll');
            });

            Route::get('/', 'PosController@index');
            Route::get('/get_pos_pagination', 'PosController@getPosPagination');
            Route::get('/types', 'PosController@posTypes');
            Route::post('/', 'PosController@create');
            Route::get('{id}', 'PosController@show');
            Route::put('{id}', 'PosController@update');
            Route::delete('{id}', 'PosController@delete');
        });

        // Tax service endpoints
        Route::group(['prefix' => 'tax'], function () {
            Route::group(['prefix' => 'tax-profile'], function () {
                Route::get('/', 'TaxMainController@getTaxProfiles');
                Route::post('/', 'TaxMainController@storeTaxProfile');
                Route::get('{id}', 'TaxMainController@getTaxProfile');
                Route::put('{id}', 'TaxMainController@updateTaxProfile');
                Route::delete('{id}', 'TaxMainController@destroyTaxProfile');
            });
            Route::get('/', 'TaxMainController@getTaxes');
            Route::post('/', 'TaxMainController@storeTax');
            Route::get('condition-types', 'TaxMainController@getTaxConditionTypes');
            Route::get('{id}', 'TaxMainController@getTax');
            Route::put('{id}', 'TaxMainController@updateTax');
            Route::delete('{id}', 'TaxMainController@destroyTax');
        });

        // Inventory service endpoints
        Route::group(['prefix' => 'inventory'], function () {
            Route::get('/product/{id}', 'ItemController@getItembyId');
            Route::post('/product', 'ItemController@updateProductDiscountPrice');
            Route::Delete('/product-delete/{id}', 'ItemController@deleteProduct');
            Route::get('/webshop/openclose/{id}', 'ShopController@CheckHours');
            Route::get('/webshop', 'ShopController@getAllShop');
            Route::get('/webshop/{id}', 'ShopController@getAllShopbyID');
            Route::get('/shop/defaultshop', 'ShopController@GetdefaultShop');
            Route::put('/shop/update-promotion/{id}', 'ShopController@updatePromotion');
            Route::put('/shop/update-DeliveryDistance/{id}', 'ShopController@updateDeliveryDistance');
            Route::put('/shop/updateOpen/{id}', 'ShopController@updateOpenClose');
            Route::put('/shop/update-location/{id}', 'ShopController@UpdateLocation');
        });
    });
    Route::group(['prefix' => 'webshop'], function () {
        Route::get('categories/webshop-brand/{brandId}/shop/{id}', 'MenuController@getCategoryByShopId');
        Route::get('main-menu/{id}/categories/webshop-brand/{brandId}/shop/{shopId}', 'MenuController@getWebshopCategoryItemsByMenuIdAndShopId');
        Route::get('main-menu/{id}/items/webshop-brand/{brandId}/shop/{shopId}', 'MenuController@getWebshopItemsByMenuIdAndShopId');
        Route::get('main-menu/{id}/category/{category}/webshop-brand/{brandId}/shop/{shopId}', 'MenuController@filterCategoryItems');
        Route::get('main-menu/{id}/submenu/times/webshop-brand/{brandId}/shop/{shopId}', 'MenuController@getWebshopMenuTimes');
    });

    Route::group(['prefix' => 'table-order'], function () {
        Route::get('categories/webshop-brand/{brandId}/shop/{id}', 'MenuController@getTableOrderCategoryByShopId');
        Route::get('main-menu/{id}/categories/webshop-brand/{brandId}/shop/{shopId}', 'MenuController@getTableOrderCategoryItemsByMenuIdAndShopId');
        Route::get('main-menu/{id}/category/{category}/webshop-brand/{brandId}/shop/{shopId}', 'MenuController@filterTableOrderCategoryItems');
    });
});
