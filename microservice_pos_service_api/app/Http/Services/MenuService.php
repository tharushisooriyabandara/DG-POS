<?php

namespace App\Http\Services;

use Exception;
use DateTimeUtility;
use App\Http\Models\Item;
use App\Http\Models\Menu;
use App\Http\Models\Category;
use App\Http\Models\MainMenu;
use App\Http\Models\ItemPrice;
use App\Http\Models\MenuHistory;
use App\Http\Models\WebshopMenu;
use App\Http\Models\CategoryMenu;
use App\Http\Models\ItemCategory;
use App\Http\Models\MainMenuMenu;
use App\Http\Models\ShopMainMenu;
use App\Http\Helpers\CommonHelper;
use App\Http\Models\ModifierGroup;
use App\Http\Services\ItemService;
use Illuminate\Support\Facades\DB;
use App\Jobs\UpdatePosWebshopMenu;
use App\Jobs\UpdateSnoozeItemList;
use App\Http\Services\ImageService;
use App\Http\Models\ShopSnoozeItem;
use Illuminate\Support\Facades\Auth;
use App\Http\Models\MainMenuSchedular;
use App\Http\Services\CategoryService;
use App\Http\Services\ModifierService;
use App\Http\Models\ModifierGroupItem;
use Illuminate\Support\Facades\Config;
use App\Http\Models\EntityDeliveryPlatform;
use App\Http\Models\ModifierGroupModifierItem;
use App\microservice_delivergate_api\Models\Shop;
use App\microservice_delivergate_api\Services\RequestHandleService;
use App\microservice_delivergate_api\Services\BaseService as BaseService;

class MenuService extends BaseService
{
    private $request_handle_service;

    public function __construct()
    {
        $this->request_handle_service = new RequestHandleService;
    }

    public function store($data)
    {
        try {
            $availability = [];
            if (isset($data['availability'])) {
                foreach ($data['availability'] as $key => $av) {
                    $availability[] = ['day_of_week' => $key, 'availability' => true, 'time_periods' => [['start_time' => $av['from'], 'end_time' => $av['to']]]];
                }
            }

            $menu = new Menu;
            if (isset($data['id'])) {
                $menu->id = $data['id'];
            }
            $menu->title = $data['title'];
            $menu->sub_title = (isset($data['sub_title']) ? $data['sub_title'] : null);
            $menu->description = $data['description'];
            $menu->status = $data['status'];
            $menu->main_menu_id = $data['main_menu'];
            $menu->service_availability = serialize($availability);
            DB::transaction(function () use ($menu, $data) {
                $menu->save();
                if (isset($data['main_menu'])) {
                    if (is_null($menu->franchise_id)) {
                        $mainMenu = MainMenu::find($data['main_menu']);
                        $menu->franchise_id = $mainMenu->masterOutlet->franchise_id;
                        $menu->save();
                    }
                    $mainMenuMenu = MainMenuMenu::firstOrNew([
                        'main_menu_id' => $data['main_menu'],
                        'menu_id' => $menu->id,
                    ]);
                    $mainMenuMenu->save();
                    if (isset($data['categories'])) {
                        $menuCat = [];
                        $categoryIds = array_unique($data['categories']);
                        foreach ($categoryIds as $key => $cat) {
                            $menuCat[] = ['menu_id' => $menu->id, 'category_id' => $cat, 'main_menu_id' => $data['main_menu']];
                        }
                        $menu->categories()->attach($menuCat);
                    }
                } elseif (isset($data['categories'])) {
                    $menuCat = [];
                    $categoryIds = array_unique($data['categories']);
                    foreach ($categoryIds as $key => $cat) {
                        $menuCat[] = ['menu_id' => $menu->id, 'category_id' => $cat, 'main_menu_id' => null];
                    }
                    $menu->categories()->attach($menuCat);
                }
            });
            CommonHelper::userLog(null, ['description' => 'Created menu titled "' . $menu->title . '"', 'event' => 'create', 'subject_type' => 'menu', 'subject_id' => $menu->id]);
            return $this->success('Successfully stored the menu.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function show($id)
    {
        try {
            $menu = Menu::find($id);
            if (is_null($menu)) {
                return $this->notFound('Menu not found');
            }
            $menu->availability = (unserialize($menu->service_availability) == false ? [] : unserialize($menu->service_availability));
            $categories = [];
            $categoryIds = [];
            foreach ($menu->categories as $key => $category) {
                if (!in_array($category->id, $categoryIds)) {
                    $categories[] = $category;
                    $categoryIds[] = $category->id;
                }
            }
            unset($menu->categories);
            $menu->categories = $categories;

            return $this->success('Menu', $menu);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function update($data, $id)
    {
        try {
            $availability = [];
            if (isset($data['availability'])) {
                foreach ($data['availability'] as $key => $av) {
                    $availability[] = ['day_of_week' => $key, 'availability' => true, 'time_periods' => [['start_time' => $av['from'], 'end_time' => $av['to']]]];
                }
            }

            $menu = Menu::find($id);
            $menu->title = $data['title'];
            $menu->sub_title = (isset($data['sub_title']) ? $data['sub_title'] : null);
            $menu->description = $data['description'];
            $menu->status = $data['status'];
            $menu->main_menu_id = $data['main_menu'];
            $menu->service_availability = serialize($availability);
            DB::transaction(function () use ($menu, $data) {
                $menu->save();
                if (isset($data['main_menu'])) {
                    $mainMenu = MainMenu::find($data['main_menu']);
                    if (is_null($menu->franchise_id)) {
                        $menu->franchise_id = $mainMenu->masterOutlet->franchise_id;
                        $menu->save();
                    }
                    $mainMenuMenu = MainMenuMenu::firstOrNew([
                        'main_menu_id' => $data['main_menu'],
                        'menu_id' => $menu->id,
                    ]);
                    $mainMenuMenu->save();
                    if (isset($data['categories'])) {
                        CategoryMenu::where('main_menu_id', $data['main_menu'])->where('menu_id', $menu->id)->delete();
                        $menuCat = [];
                        $categoryIds = array_unique($data['categories']);
                        foreach ($categoryIds as $key => $cat) {
                            $menuCat[] = ['menu_id' => $menu->id, 'category_id' => $cat, 'main_menu_id' => $data['main_menu']];
                        }
                        //$menu->categories()->detach();
                        $menu->categories()->attach($menuCat);

                        UpdatePosWebshopMenu::dispatch(['mainMenuId' => /*$data['main_menu']*/null, 'shopId' => $mainMenu->masterOutlet->id, 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
                    }
                } elseif (isset($data['categories'])) {
                    $menuCat = [];
                    CategoryMenu::where('menu_id', $menu->id)->whereNull('main_menu_id')->delete();
                    $categoryIds = array_unique($data['categories']);
                    foreach ($categoryIds as $key => $cat) {
                        $menuCat[] = ['menu_id' => $menu->id, 'category_id' => $cat, 'main_menu_id' => null];
                    }
                    //$menu->categories()->detach();
                    $menu->categories()->attach($menuCat);
                }
            });
            CommonHelper::userLog(null, ['description' => 'Updated menu', 'event' => 'update', 'subject_type' => 'menu', 'subject_id' => $menu->id]);
            return $this->success('Successfully updated the menu.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function menuCategories($id, $main_menu)
    {
        try {
            $menu = Menu::find($id);
            if (is_null($menu)) {
                return $this->notFound('Menu not found');
            }
            if (is_null($main_menu)) {
                $categories = [];
                $categoryIds = [];
                foreach ($menu->categories as $key => $category) {
                    if (!in_array($category->id, $categoryIds)) {
                        $categories[] = $category;
                        $categoryIds[] = $category->id;
                    }
                }
                $menu->categoryList = $categories;
            } else {
                $menu->categoryList = $menu->categoryList($main_menu);
            }
            return $this->success('Menu categories', $menu->categoryList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function menuItems($id, $main_menu)
    {
        try {
            $menu = Menu::find($id);
            if (is_null($menu)) {
                return $this->notFound('Menu not found');
            }
            $categories = $menu->categoryList($main_menu);
            $items = [];
            foreach ($categories as $key => $category) {
                foreach ($category->itemList($main_menu) as $key => $item) {
                    if (!is_null($main_menu)) {
                        $itemPrice = $item->prices->where('main_menu_id', $main_menu);
                        if (count($itemPrice) > 0) {
                            $item->price = $itemPrice->first()->price;
                        }
                    }
                    $items[] = $item;
                }
            }
            return $this->success('Menu items', $items);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function destroy($id, $main_menu)
    {
        try {
            $menu = Menu::find($id);
            if (is_null($menu)) {
                return $this->notFound('Menu not found');
            }
            DB::transaction(function () use ($menu, $id, $main_menu) {
                if (is_null($main_menu)) {
                    CategoryMenu::where('menu_id', $id)->delete();
                    MainMenuMenu::where('menu_id', $id)->delete();
                    WebshopMenu::where('submenu_id', $id)->delete();
                    $menu->delete();
                } else {
                    CategoryMenu::where('menu_id', $id)->where('main_menu_id', $main_menu)->delete();
                    MainMenuMenu::where('menu_id', $id)->where('main_menu_id', $main_menu)->delete();
                    WebshopMenu::where('submenu_id', $id)->where('main_menu_id', $main_menu)->delete();
                }
            });
            CommonHelper::userLog(null, ['description' => 'Deleted menu', 'event' => 'delete', 'subject_type' => 'menu', 'subject_id' => $menu->id]);
            return $this->success('Successfully deleted the menu.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function uploadRemoteMenu()
    {
        try {
            $menu_list = [];
            $menus = Menu::where('status', 1)->get();
            foreach ($menus as $key => $menu) {
                $menu_list[] = [
                    'id' => $menu->id,
                    'title' => ['translations' => ['en_gb' => $menu->title]],
                    'subtitle' => ['translations' => ['en_gb' => $menu->sub_title]],
                    'service_availability' => (unserialize($menu->service_availability) == false ? [] : unserialize($menu->service_availability)),
                    'category_ids' => $menu->categories->pluck('id')->toArray(),
                ];
            }
            $category_list = [];
            $categories = Category::where('status', 1)->get();
            foreach ($categories as $key => $category) {
                $item_array = [];
                foreach ($category->items as $key => $itm) {
                    $item_array[] = ['type' => 'ITEM', 'id' => $itm->id];
                }

                $category_list = [];
                $categories = Category::where('status', 1)->get();
                foreach ($categories as $key => $category) {
                    $item_array = [];
                    foreach ($category->items as $key => $itm) {
                        $item_array[] = ['type' => 'ITEM', 'id' => $itm->id];
                    }
                    $category_list[] = [
                        'id' => $category->id,
                        'title' => ['translations' => ['en_gb' => $category->title]],
                        'subtitle' => ['translations' => ['en_gb' => $category->sub_title]],
                        'entities' => $item_array,
                    ];
                }
            }
            $item_list = [];
            $items = Item::where('status', 1)->get();
            $item_availability = [
                ['day_of_week' => 'monday', 'availability' => true, 'time_periods' => [['start_time' => '08:00', 'end_time' => '23:00']]],
                ['day_of_week' => 'tuesday', 'availability' => true, 'time_periods' => [['start_time' => '08:00', 'end_time' => '23:00']]],
                ['day_of_week' => 'wednesday', 'availability' => true, 'time_periods' => [['start_time' => '08:00', 'end_time' => '23:00']]],
                ['day_of_week' => 'thursday', 'availability' => true, 'time_periods' => [['start_time' => '08:00', 'end_time' => '23:00']]],
                ['day_of_week' => 'friday', 'availability' => true, 'time_periods' => [['start_time' => '08:00', 'end_time' => '23:00']]],
                ['day_of_week' => 'saturday', 'availability' => true, 'time_periods' => [['start_time' => '08:00', 'end_time' => '23:00']]],
                ['day_of_week' => 'sunday', 'availability' => true, 'time_periods' => [['start_time' => '08:00', 'end_time' => '23:00']]],
            ];
            foreach ($items as $key => $item) {
                $item_array = [];
                foreach ($category->items as $key => $itm) {
                    $item_array[] = ['type' => 'ITEM', 'id' => $itm->id];
                }
                $item_list[] = [
                    'id' => $item->id,
                    'title' => ['translations' => ['en_gb' => $item->title]],
                    'description' => ['translations' => ['en_gb' => $item->description]],
                    'image_url' => '',
                    'price_info' => ['price' => $item->price(), 'overrides' => null],
                    'quantity_info' => ['quantity' => [
                        'min_permitted' => null,
                        'max_permitted' => null,
                        'default_quantity' => null,
                        'charge_above' => null,
                        'refund_under' => null,
                    ], 'overrides' => [
                        'context_type' => 'MODIFIER_GROUP',
                        'context_value' => 'Choose-sauces',
                        'quantity' => [
                            'min_permitted' => null,
                            'max_permitted' => null,
                            'default_quantity' => null,
                            'charge_above' => null,
                            'refund_under' => null,
                        ],
                    ]],
                    'suspension_info' => null,
                    'modifier_group_ids' => '',
                    'tax_info' => ['tax_rate' => null, 'vat_rate_percentage' => null],
                    'nutritional_info' => ['calories' => null, 'kilojoules' => null],
                    'dish_info' => ['classifications' => ['can_serve_alone' => true, 'is_vegetarian' => false, 'alcoholic_items' => null]],
                    'visibility_info' => ['hours' => ['start_date' => null, 'end_date' => null, 'hours_of_week' => $item_availability]],
                    'tax_label_info' => null,
                ];
            }
            return [
                'menus' => $menu_list,
                'categories' => $category_list,
                'items' => $item_list,
                'modifier_groups' => [],
                'display_options' => [],
            ];
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function fetchMenu($data)
    {
        try {
            DB::transaction(function () use ($data) {
                $itemService = new ItemService;
                $categoryService = new CategoryService;
                $modifierService = new ModifierService;
                $main_menu = (isset($data['main_menu']) ? $data['main_menu'] : 1);
                $remoteMap = [];
                if (isset($data['menu-data']['items']) && count(Item::all()) == 0) {
                    $savedIds = [];
                    foreach ($data['menu-data']['items'] as $iKey => $item) {
                        $inputData = [
                            'id' => (300000 + $iKey + 1),
                            'image_url' => $item['image_url'],
                            'source' => 'DELIVERY_PLATFORM',
                            'title' => $item['title'],
                            'description' => $item['description'],
                            'tax' => $item['tax_rate'],
                            'price' => ($item['price'] / 100),
                            'contains_alcohol' => $item['contains_alcohol'],
                            'main_menu' => $main_menu,
                            'status' => 1,
                        ];
                        $remoteMap[$item['remote_id']] = (300000 + $iKey + 1);
                        $response = $itemService->store($inputData);
                        if ($response->getStatusCode() == 200) {
                            $response = (json_decode($response->getContent()))->data;
                            $savedIds[] = $response->id;
                        }
                    }
                    if (isset($data['main_menu']) && $data['main_menu'] != '') {
                        $mainMenu = MainMenu::find($data['main_menu']);
                        if (is_null($mainMenu)) {
                            $mainMenu = new MainMenu;
                            $mainMenu->id = $data['main_menu'];
                            $mainMenu->name = 'Main menu';
                        }
                        $mainMenu->item_ids = serialize($savedIds);
                        $mainMenu->item_count = count($savedIds);
                        $tmpPlatforms = [];
                        $mainMenu->save();
                    }
                }
                $catMap = [];
                if (isset($data['menu-data']['categories']) && count(Category::all()) == 0) {
                    foreach ($data['menu-data']['categories'] as $iKey => $category) {
                        $ids = [];
                        foreach ($category['items'] as $key => $catItm) {
                            if (isset($remoteMap[$catItm])) {
                                $ids[] = $remoteMap[$catItm];
                            }
                        }
                        $inputData = [
                            'id' => (200000 + $iKey + 1),
                            'title' => $category['title'],
                            'description' => $category['description'],
                            'status' => $category['status'],
                            'main_menu' => $main_menu,
                            'items' => $ids,
                        ];
                        $catMap[$category['remote_id']] = (200000 + $iKey + 1);
                        $categoryService->store($inputData);
                    }
                } elseif (isset($data['menu-data']['categories']) && count(Category::all()) != 0) {
                    foreach ($data['menu-data']['categories'] as $iKey => $category) {
                        $ids = [];
                        foreach ($category['items'] as $key => $catItm) {
                            $entityDeliveryPlatformItem = EntityDeliveryPlatform::where('external_item_id', $catItm)->whereNotNull('entity_id')->get();
                            $tmpCategory = Category::where('title', $category['title'])->get();
                            if (count($tmpCategory) > 0) {
                                $tmpCategory = $tmpCategory->first();
                            } else {
                                $tmpCategory = new Category;
                                $tmpCategory->title = $category['title'];
                                $tmpCategory->save();
                            }
                            if (count($entityDeliveryPlatformItem) > 0) {
                                $entityDeliveryPlatformItem = $entityDeliveryPlatformItem->first();
                                if (count($entityDeliveryPlatformItem->item->categories) == 0) {
                                    $itmCat = ItemCategory::firstOrNew([
                                        'category_id' => $tmpCategory->id,
                                        'item_id' => $entityDeliveryPlatformItem->entity_id,
                                        'main_menu_id' => $main_menu,
                                    ]);
                                    $itmCat->save();
                                }
                            }
                        }
                    }
                }

                if (isset($data['menu-data']['menus']) && count(Menu::all()) == 0) {
                    foreach ($data['menu-data']['menus'] as $iKey => $menu) {
                        if (isset($menu['availability'])) {
                            $availability = $menu['availability'];
                        } else {
                            $availability = [
                                'monday' => ['from' => '08:00', 'to' => '23:00'],
                                'tuesday' => ['from' => '08:00', 'to' => '23:00'],
                                'wednesday' => ['from' => '08:00', 'to' => '23:00'],
                                'thursday' => ['from' => '08:00', 'to' => '23:00'],
                                'friday' => ['from' => '08:00', 'to' => '23:00'],
                                'saturday' => ['from' => '08:00', 'to' => '23:00'],
                                'sunday' => ['from' => '08:00', 'to' => '23:00'],
                            ];
                        }

                        $ids = [];
                        foreach ($menu['categories'] as $key => $menuCat) {
                            if (isset($catMap[$menuCat])) {
                                $ids[] = $catMap[$menuCat];
                            }
                        }

                        $inputData = [
                            'id' => (400000 + $iKey + 1),
                            'title' => $menu['title'],
                            'description' => $menu['description'],
                            'status' => $menu['status'],
                            'main_menu' => $main_menu,
                            'categories' => $ids,
                            'availability' => $availability,
                        ];
                        $this->store($inputData);
                    }
                }

                if (isset($data['menu-data']['modifiers'])) {
                    foreach ($data['menu-data']['modifiers'] as $iKey => $item) {
                        $entity = ModifierGroup::where('remote_id', $item['remote_id'])->get()->first();

                        if (is_null($entity)) {
                            $item['main_menu_id'] = $main_menu;
                            $modifierService->store($item);
                        } else {
                            $modifierService->update($item, $entity->id);
                        }
                    }
                }

                if (isset($data['menu-data']['items'])) {
                    $externalIds = [];
                    foreach ($data['menu-data']['items'] as $iKey => $item) {
                        $entity = EntityDeliveryPlatform::where('external_item_id', $item['remote_id'])->where('delivery_platform_id', $item['delivery_platform'])->get();
                        $externalIds[] = $item['remote_id'];
                        if (isset($remoteMap[$item['remote_id']])) {
                            $item['matching_id'] = $remoteMap[$item['remote_id']];
                        }
                        $item['source'] = 'UPLOAD';
                        if (count($entity) > 0) {
                            $itemService->updateDeliveryPlatformItem($item, $entity->first()->id);
                        } else {
                            $itemService->createDeliveryPlatformItem($item);
                        }
                    }
                    //EntityDeliveryPlatform::where('type', 'ITEM')->whereNotIn('external_item_id', $externalIds)->where('delivery_platform_id', $data['platform']['id'])->delete();
                }

                $modifierService->syncPosModifiers($data['platform']['id']);

                if (isset($data['main_menu']) && $data['main_menu'] != '') {
                    $platforms = ItemPrice::where('main_menu_id', $data['main_menu'])->whereNotNull('delivery_platform_id')->pluck('delivery_platform_id')->toArray();
                    $platforms = array_unique($platforms);
                    $mainMenu = MainMenu::find($data['main_menu']);
                    $platforms = array_values($platforms);
                    $mainMenu->platform_ids = serialize($platforms);
                    $mainMenu->save();
                }
            });
            return $this->success('Successfully fetched the menu.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function getPosMenu($data)
    {
        try {
            $mainMenu = MainMenu::find($data['main_menu']);
            $menus = Menu::where('main_menu_id', $mainMenu->id)->whereIn('id', $mainMenu->menus->pluck('id')->toArray())->where('status', 1)->orderBy('from', 'ASC')->get();
            $menuList = [];
            foreach ($menus as $key => $menu) {
                $categories = $menu->categoriesWithMainMenu;
                $mainMenus = $menu->mainMenus;
                unset($menu->categories);
                unset($menu->categoriesWithMainMenu);
                $menu->categories = $categories;
                $menuList[] = $menu;
            }
            $menuList = array_values($menuList);

            $categories = $mainMenu->categories->where('status', 1)->fresh('menus', 'items', 'children', 'parent');
            foreach ($categories as $key => $category) {
                foreach ($category->items as $keyi => $item) {
                    $tmpItemCat = ItemCategory::where('item_id', $item->id)->where('category_id', $category->id)->where('main_menu_id', $data['main_menu'])->get();
                    if (count($tmpItemCat)>0) {
                        $item->pivot->main_menu_id = (int)$data['main_menu'];
                    }
                }
            }
            $mainMenuCatItemIds = [];
            foreach ($categories as $key => $cat) {
                if (count($cat->itemList($data['main_menu']))>0) {
                    $tmpCatArray = $cat->itemList($data['main_menu'])->pluck('id')->toArray();
                    $mainMenuCatItemIds = array_merge($mainMenuCatItemIds, $tmpCatArray);
                }
                foreach ($cat->items as $key => $value) {
                    $value->entityDeliveryPlatform;
                    $value->prices;
                }
            }
            $entityItems = EntityDeliveryPlatform::all()->fresh('item', 'prices');
            $items = Item::where('status', 1)->get()->fresh('categories', 'entityDeliveryPlatform', 'prices');
            $modifiers = ModifierGroup::where('status', 1)->where('main_menu_id', $mainMenu->id)->get()->fresh('items', 'mainItems');
            return $this->success('Pos menu.', ['items' => $items, 'menus' => $menuList, 'categories' => $categories, 'modifiers' => $modifiers, 'main_menu' => $mainMenu, 'main_menu_items' => $mainMenu->items(), 'entityItems' => $entityItems, 'mainMenuCatItemIds' => $mainMenuCatItemIds]);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function getMappingMenuList($data, $query = null)
    {
        try {
            $main_menu = (isset($data['main_menu']) ? MainMenu::find($data['main_menu']) : null);
            $outletId = (isset($data['outlet_id']) ? MainMenu::find($data['outlet_id']) : null);
            $item_ids = [];
            if (is_null($query)) {
                $itemList = Item::all()->fresh('entityDeliveryPlatform', 'prices', 'categories');
            } else {
                $itemList = Item::where('title', 'LIKE', '%'.$query.'%')->get()->fresh('entityDeliveryPlatform', 'prices', 'categories');
            }
            if (!is_null($main_menu)) {
                $item_ids = (is_null($main_menu->item_ids) ? [] : unserialize($main_menu->item_ids));
            }
            $mainMenuDPs = [];
            $shopIds = [];
            if (!is_null($main_menu)) {
                $shopIds = ShopMainMenu::where('main_menu_id', $main_menu->id)->pluck('shop_id')->toArray();
            }

            $platformResponse = $this->request_handle_service->getRequst(null, '/api/v1/admin/platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platforms = (json_decode($platformResponse->getBody()))->data;
            $platformList = [];
            foreach ($platforms as $key => $platform) {
                $platformList[$platform->id] = $platform->logo;
            }
            $masterOutletDeliveryIds = [];
            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $deliveryPlatforms = (json_decode($response->getBody()))->data;
            $deliveryPlatformList = [];
            $deliveryPlatformIds = [];
            foreach ($deliveryPlatforms as $key => $deliveryPlatform) {
                if (!is_null($main_menu) && (is_null($outletId)?($deliveryPlatform->outlet_id == $main_menu->master_outlet):($deliveryPlatform->outlet_id == $outletId))) {
                    $masterOutletDeliveryIds[] = $deliveryPlatform->id;
                }
                if (!is_null($main_menu) && in_array($deliveryPlatform->outlet_id, $shopIds)) {
                    $mainMenuDPs[] = $deliveryPlatform->id;
                }
                $deliveryPlatformIds[] = $deliveryPlatform->id;
                if (true || in_array($deliveryPlatform->platform_id, Config::get('common.fetchable_platforms'))) {
                    $deliveryPlatformList[$deliveryPlatform->id] = ['id' => $deliveryPlatform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $deliveryPlatform->name))), 'logo' => $platformList[$deliveryPlatform->platform_id]];
                }
            }

            foreach ($itemList as $key => $item) {
                $item->masterEntityDeliveryPlatforms = $item->entityDeliveryPlatform->whereIn('delivery_platform_id', $masterOutletDeliveryIds);
                $item->prices = $item->prices->where('main_menu_id', (is_null($main_menu) ? null : $main_menu->id))->whereNotNull('delivery_platform_id');
                $availablePlatformIds = $item->prices->pluck('delivery_platform_id')->toArray();
                $item->availablePlatformIds = $availablePlatformIds;
                $platform_urls = [];
                $matchedPlatform_urls = [];
                $item->categoryList = $item->categoriesByMainMenu((is_null($main_menu) ? null : $main_menu->id));
                $itemPrice = $item->prices->where('main_menu_id', (is_null($main_menu) ? null : $main_menu->id))->where('delivery_platform_id', null);
                if (count($itemPrice) > 0) {
                    $item->price = $itemPrice->first()->price;
                }
                $modifiers = null;
                $allergies = null;
                $tmpPlatforms = [];
                $tmpMatchedPlatforms = [];
                $is_modifier = false;
                $masterEntityDeliveryPlatforms = [];
                foreach ($item->masterEntityDeliveryPlatforms as $key => $masterEntity) {
                    $modifiers = $masterEntity->modifiers;
                    $masterEntity->allergies = (unserialize($masterEntity->allergies) == false ? [] : unserialize($masterEntity->allergies));
                    $allergies = $masterEntity->allergies;
                    if (!$is_modifier) {
                        $is_modifier = CommonHelper::isModifier($masterEntity->external_item_id);
                    }
                    $tmpPrice = $item->prices->where('main_menu_id', (is_null($main_menu) ? null : $main_menu->id))->where('delivery_platform_id', $masterEntity->delivery_platform_id);
                    if (count($tmpPrice) > 0) {
                        $masterEntity->price = $tmpPrice->first()->price;
                    }
                    if (!in_array($masterEntity->delivery_platform_id, $tmpPlatforms)) {
                        $platform_urls[] = $deliveryPlatformList[$masterEntity->delivery_platform_id];
                        $tmpPlatforms[] = $masterEntity->delivery_platform_id;
                    }
                    if (in_array($masterEntity->delivery_platform_id, $availablePlatformIds)) {
                        $masterEntityDeliveryPlatforms[] = $masterEntity;
                    }
                }
                foreach ($item->entityDeliveryPlatform as $key1 => $entity) {
                    if (in_array($entity->delivery_platform_id, $deliveryPlatformIds)) {
                        if (!in_array($entity->delivery_platform_id, $tmpMatchedPlatforms)) {
                            $matchedPlatform_urls[] = $deliveryPlatformList[$entity->delivery_platform_id];
                            $tmpMatchedPlatforms[] = $entity->delivery_platform_id;
                        }
                        $entity->allergies = (is_array($entity->allergies) ? $entity->allergies : (unserialize($entity->allergies) == false ? [] : unserialize($entity->allergies)));
                    }
                }

                if (is_null($main_menu)) {
                    $item->match_found = count($deliveryPlatformList) == count(array_unique($item->entityDeliveryPlatform->pluck('delivery_platform_id')->toArray()));
                } else {
                    $item->match_found = count($mainMenuDPs) == count(array_unique($item->entityDeliveryPlatform->whereIn('delivery_platform_id', $mainMenuDPs)->pluck('delivery_platform_id')->toArray()));
                }
                $item->modifiers = $modifiers;
                $item->allergies = $allergies;
                $item->platform_urls = $platform_urls;
                $item->matchedPlatform_urls = $matchedPlatform_urls;
                $item->is_modifier = $is_modifier;
            }
            return $this->success('Items', ['selectedIds' => $item_ids, 'itemList' => $itemList]);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function getMappingMenuList2($data)
    {
        try {
            $platformResponse = $this->request_handle_service->getRequst(null, '/api/v1/admin/platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platforms = (json_decode($platformResponse->getBody()))->data;
            $platformList = [];
            foreach ($platforms as $key => $platform) {
                $platformList[$platform->id] = $platform->logo;
            }

            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $deliveryPlatforms = (json_decode($response->getBody()))->data;
            $deliveryPlatformList = [];
            foreach ($deliveryPlatforms as $key => $deliveryPlatform) {
                $deliveryPlatformList[$deliveryPlatform->id] = ['id' => $deliveryPlatform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $deliveryPlatform->name))), 'logo' => $platformList[$deliveryPlatform->platform_id]];
            }
            $itemList = [];
            $platformItems = EntityDeliveryPlatform::all();
            foreach ($platformItems as $key => $item) {
                $tmpItm = $item;
                $tmpItm->allergies = (unserialize($tmpItm->allergies) == false ? [] : unserialize($tmpItm->allergies));

                if (!isset($itemList[$item->item_name])) {
                    $pos_price = (is_null($item->item) ? '' : $item->item->price);
                    if (isset($data['main_menu'])) {
                        if (!is_null($item->item)) {
                            $tmpPrice = $item->item->prices->where('main_menu_id', $data['main_menu']);
                            if (count($tmpPrice) != 0) {
                                $pos_price = ($tmpPrice->first())->price;
                            }
                        }
                    }
                    $itemList[$item->item_name] = ['platform_item' => $item->item_name, 'pos_item' => (is_null($item->item) ? '' : $item->item->title), 'pos_price' => $pos_price, 'matched_items' => (is_null($item->item) ? 0 : 1), 'match_found' => (is_null($item->item) ? false : (count($deliveryPlatformList) == 1)), 'platforms' => [$deliveryPlatformList[$item->delivery_platform_id]], 'platform_items' => [$tmpItm]];
                } else {
                    $itemList[$item->item_name]['platforms'][] = $deliveryPlatformList[$item->delivery_platform_id];
                    $itemList[$item->item_name]['platform_items'][] = $tmpItm;
                    $itemList[$item->item_name]['matched_items'] = $itemList[$item->item_name]['matched_items'] + (is_null($item->item) ? 0 : 1);
                    $itemList[$item->item_name]['match_found'] = (count($deliveryPlatformList) == $itemList[$item->item_name]['matched_items']);
                }
            }
            return $this->success('Menu', ['map_items' => $itemList]);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function filterMappedItems($data)
    {
        try {
            $itemService = new ItemService;
            $results = $this->getMappingMenuList($data, (isset($data['query'])?$data['query']:null));
            if (!isset($data['status']) || $data['status'] == 'ALL') {
                return $results;
            }
            $contents = json_decode($results->getContent());
            $status = ($data['status'] == 'match_found' ? true : false);
            $filteredValues = [];

            foreach ($contents->data->itemList as $key => $result) {
                if ($result->match_found == $status) {
                    $filteredValues[] = $result;
                }
            }
            return $this->success('Menu', ['map_items' => $filteredValues]);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function getMainMenu($brandId = null)
    {
        try {
            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platforms = (json_decode($response->getBody()))->data;
            $user = Auth::guard('api')->user();
            if (is_null($user) || in_array($user->roles->first()->id, [1, 2])) {
                $menus = MainMenu::all();
            } elseif (in_array($user->roles->first()->id, [3])) {
                $shopIds = Shop::whereIn('franchise_id', $user->franchises->pluck('id')->toArray())->pluck('id')->toArray();
                $mainMenuIds = ShopMainMenu::whereIn('shop_id', $shopIds)->pluck('main_menu_id')->toArray();
                $menus = MainMenu::whereIn('id', $mainMenuIds)->get();
            } else {
                $shopIds = $user->shops->pluck('id')->toArray();
                $mainMenuIds = ShopMainMenu::whereIn('shop_id', $shopIds)->pluck('main_menu_id')->toArray();
                $menus = MainMenu::whereIn('id', $mainMenuIds)->get();
            }
            foreach ($menus as $key => $menu) {
                $menu->item_ids = (unserialize($menu->item_ids) == false ? [] : unserialize($menu->item_ids));
                $menu->platform_ids = ((is_null($menu->platform_ids) || unserialize($menu->platform_ids) == false) ? [] : unserialize($menu->platform_ids));
                $menu->shops = $menu->shops;
                $menu->activeShops = $menu->activeShops();
                if (count($menu->platform_ids)) {
                    $masterDeliveryPlatforms = [];
                    $tmpPlatforms = [];
                    foreach ($platforms as $key1 => $platform) {
                        if (in_array($platform->id, $menu->platform_ids)) {
                            if (!is_null($menu->masterOutlet) && $menu->masterOutlet->id==$platform->outlet_id) {
                                $masterDeliveryPlatforms[] = $platform;
                                if (!is_null($brandId)) {
                                    $masterOutletDp = DB::table('delivery_platform')->where('webshop_brand_id', $brandId)->where('outlet_id', $menu->masterOutlet->id)->first();
                                    if (!is_null($masterOutletDp)) {
                                        $menu->masterOutlet->selected_menu = $masterOutletDp->selected_menu;
                                    }
                                }
                            }
                            $tmpPlatforms[] = $platform;
                        }
                    }
                    $menu->delivery_platforms = $tmpPlatforms;
                    $menu->master_delivery_platforms = $masterDeliveryPlatforms;
                }
            }
            return $this->success('Main menu list', ['menus' => $menus, 'total_items' => count(Item::all())]);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function storeMainMenu($data)
    {
        try {
            $menu = new MainMenu;
            $menu->name = $data['name'];
            $menu->master_outlet = $data['master_outlet'];
            $menu->description = (isset($data['description']) ? $data['description'] : null);
            $menu->platform_ids = (isset($data['platform_ids']) ? serialize($data['platform_ids']) : null);
            $menu->status = (isset($data['status']) ? $data['status'] : 'INACTIVE');
            DB::transaction(function () use (&$menu, $data) {
                $image_service = new ImageService;
                if (isset($data['image']) && $data['image'] != '') {
                    $path = $image_service->uploadImageToCloud($data['image'], 'banners');
                    $menu->image_url = $path;
                }
                $menu->save();
                if (isset($data['outlets'])) {
                    $menu->shops()->attach($data['outlets']);
                }

                $itemCategories = ItemCategory::whereNull('main_menu_id')->get();
                foreach ($itemCategories as $key => $itemCategory) {
                    $itemCategoryMenu = $itemCategory->replicate();
                    $itemCategoryMenu->main_menu_id = $menu->id;
                    $itemCategoryMenu->save();
                }
            });
            CommonHelper::userLog(null, ['description' => 'Created main menu named "' . $menu->name . '"', 'event' => 'create', 'subject_type' => 'main_menu', 'subject_id' => $menu->id]);
            return $this->success('Successfully stored the menu.', $menu->fresh('shops'));
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function showMainMenu($id, $brandId = null)
    {
        try {
            $menu = MainMenu::find($id);
            if (is_null($menu)) {
                return $this->notFound('Menu not found');
            }
            foreach ($menu->shops as $key => $shop) {
                $shop->availability = unserialize($shop->service_availability);
                if (!is_null($brandId)) {
                    $dp = DB::table('delivery_platform')->where('webshop_brand_id', $brandId)->where('outlet_id', $shop->id)->first();
                    if (!is_null($dp)) {
                        $shop->selected_menu = $dp->selected_menu;
                    }
                }
            }
            $menu->item_ids = (unserialize($menu->item_ids) == false ? [] : unserialize($menu->item_ids));
            $menu->platform_ids = ((is_null($menu->platform_ids) || unserialize($menu->platform_ids) == false) ? [] : unserialize($menu->platform_ids));

            $menus = [];
            foreach ($menu->menus as $key => $subMenu) {
                $menus[$subMenu->id] = ['id' => $subMenu->id, 'title' => $subMenu->title, 'item_ids' => (is_null($subMenu->item_ids)?[]:unserialize($subMenu->item_ids)), 'item_count' => $subMenu->item_count];
            }
            unset($menu->menus);
            $menu->menus = $menus;
            return $this->success('Menu', ['menu' => $menu, 'total_items' => count(Item::all())]);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function showMainMenuMenus($id)
    {
        try {
            $mainMenu = MainMenu::find($id);
            $menus = $mainMenu->menus->where('main_menu_id', $mainMenu->id)->where('franchise_id', (is_null($mainMenu->masterOutlet)?0:$mainMenu->masterOutlet->franchise_id));
            $menuList = [];
            foreach ($menus as $key => $menu) {
                $menu->service_availability = (unserialize($menu->service_availability) == false ? [] : unserialize($menu->service_availability));
                $menuList[] = $menu;
            }
            return $this->success('Menus', $menuList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function updateMainMenu($data, $id)
    {
        try {
            $menu = MainMenu::find($id);
            if (is_null($menu)) {
                return $this->notFound('Menu not found');
            }
            $menu->name = (isset($data['name']) ? $data['name'] : $menu->name);
            $menu->master_outlet = (isset($data['master_outlet']) ? $data['master_outlet'] : $menu->master_outlet);
            $menu->description = (isset($data['description']) ? $data['description'] : $menu->description);
            $menu->platform_ids = (isset($data['platform_ids']) ? serialize($data['platform_ids']) : $menu->platform_ids);
            $menu->status = (isset($data['status']) ? $data['status'] : $menu->status);
            DB::transaction(function () use (&$menu, $data) {
                $image_service = new ImageService;
                if (isset($data['image']) && $data['image'] != '') {
                    $path = $image_service->uploadImageToCloud($data['image'], 'banners');
                    $menu->image_url = $path;
                }
                $menu->save();
                if (isset($data['outlets'])) {
                    $menu->shops()->detach();
                    $menu->shops()->attach($data['outlets']);
                }
            });
            CommonHelper::userLog(null, ['description' => 'Updated main menu', 'event' => 'update', 'subject_type' => 'main_menu', 'subject_id' => $menu->id]);
            $shopIds = $menu->shops->pluck('id')->toArray();
            // UpdateSnoozeItemList::dispatch(['shopIds' => $shopIds, 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
            return $this->success('Successfully updated the menu.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function updateMainMenuItems($data, $id)
    {
        try {
            $mainMenu = MainMenu::find($id);
            if (is_null($mainMenu)) {
                return $this->notFound('Menu not found');
            }
            if (isset($data['menu_id'])) {
                $menu = Menu::find($data['menu_id']);
                $menu->item_ids = (isset($data['item_ids']) ? serialize($data['item_ids']) : serialize([]));
                $menu->item_count = (isset($data['item_ids']) ? count($data['item_ids']) : 0);
                $menu->save();

                $allItemIds = [];
                $menus = Menu::where('main_menu_id', $id)->where('status', 1)->whereNotNull('item_ids')->get();
                foreach ($menus as $key => $menu) {
                    $allItemIds = array_merge($allItemIds, unserialize($menu->item_ids));
                }

                $allItemIds = array_values(array_unique($allItemIds));
                $mainMenu->item_ids = serialize($allItemIds);
                $mainMenu->item_count = count($allItemIds);
            } else {
                $allItemIds = (isset($data['item_ids']) ? $data['item_ids'] : []);
                if (count($mainMenu->menus)==0) {
                    $mainMenu->item_ids = serialize($allItemIds);
                    $mainMenu->item_count = count($allItemIds);
                }
            }
            $mainMenu->status = (isset($data['status']) ? $data['status'] : $mainMenu->status);
            $platformIds = ItemPrice::where('main_menu_id', $id)->pluck('delivery_platform_id')->toArray();
            $platformIds = array_values(array_unique($platformIds));
            $mainMenu->platform_ids = serialize($platformIds);
            DB::transaction(function () use ($mainMenu, $data) {
                $mainMenu->save();
            });
            CommonHelper::userLog(null, ['description' => 'Updated main menu items', 'event' => 'update', 'subject_type' => 'main_menu', 'subject_id' => $mainMenu->id]);
            $shopIds = $mainMenu->shops->pluck('id')->toArray();
            // UpdateSnoozeItemList::dispatch(['shopIds' => $shopIds, 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
            return $this->success('Successfully updated the menu.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function updateMainMenuPlatforms($data, $id)
    {
        try {
            $menu = MainMenu::find($id);
            if (is_null($menu)) {
                return $this->notFound('Menu not found');
            }
            unset($data['order_flow_version']);
            $menu->platform_ids = serialize($data);
            $menu->status = 'ACTIVE';
            DB::transaction(function () use ($menu, $data) {
                $menu->save();
            });
            CommonHelper::userLog(null, ['description' => 'Updated main menu platforms', 'event' => 'update', 'subject_type' => 'main_menu', 'subject_id' => $menu->id]);
            return $this->success('Successfully updated the menu platforms.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function destroyMainMenu($id)
    {
        try {
            $shopIds = ShopMainMenu::where('main_menu_id', $id)->pluck('shop_id')->toArray();
            MainMenu::where('id', $id)->delete();
            ShopMainMenu::where('main_menu_id', $id)->delete();
            MainMenuMenu::where('main_menu_id', $id)->delete();
            CategoryMenu::where('main_menu_id', $id)->delete();
            ItemPrice::where('main_menu_id', $id)->delete();
            CommonHelper::userLog(null, ['description' => 'Deleted main menu', 'event' => 'delete', 'subject_type' => 'main_menu', 'subject_id' => $id]);
            // UpdateSnoozeItemList::dispatch(['shopIds' => $shopIds, 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
            return $this->success('Successfully deleted the Menu');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getWizardItems($data)
    {
        try {
            $itemList = [];
            $items = Item::where('status', 1)->get();
            foreach ($items as $key => $item) {
                $itemList[$item->id] = $item->title;
            }
            return $this->success('Items', $itemList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getWizardCategories($data)
    {
        try {
            $categoryList = [];
            $categories = Category::where('status', 1)->get();
            foreach ($categories as $key => $category) {
                $categoryList[$category->id] = $category->title;
            }
            return $this->success('Categories', $categoryList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getWizardModifiers($data)
    {
        try {
            $response = $this->getWizardPlatforms([]);
            $platforms = (array) (json_decode($response->getContent()))->data;
            $modifierList = [];
            if (isset($data['q'])) {
                $modifiers = ModifierGroup::whereIn('status', [1, 0])->where('title', 'like', '%'.$data['q'].'%');
            } else {
                $modifiers = ModifierGroup::whereIn('status', [1, 0]);
            }
            if (isset($data['main_menu'])) {
                $modifiers = $modifiers->where('main_menu_id', $data['main_menu']);
            }
            $modifiers = $modifiers->get();
            foreach ($modifiers as $key => $modifier) {
                $title = $modifier->title;
                $mItem = '';
                $platform = [];
                $mItemArray = [];
                $mItemPriceArray = [];
                $modifierArray = [];
                $formatedArray = [];
                if (count($modifier->items->whereNotNull('platform')) > 0) {
                    foreach ($modifier->items->whereNotNull('platform') as $key1 => $itm) {
                        $itm->display_price = ($itm->price == 0 ? 0 : $itm->price / 100);
                        $itm->item = $itm->item()->whereNotNull('entity_id')->first();
                        if ((!is_null($itm->item) && !in_array($itm->item->item_name, $mItemArray)) && !in_array($itm->item_id.'-'.$itm->price, $mItemPriceArray)) {
                            if (!is_null($itm->item)) {
                                $mItem .= ($mItem == '' ? '(' : '') . $itm->item->item_name;
                                $mItemArray[] = $itm->item->item_name;
                                $mItemPriceArray[] = $itm->item_id.'-'.$itm->price;
                                if (is_null($itm->platform)) {
                                    //$platform[] = $itm->item->delivery_platform_id;
                                } else {
                                    $platform[$itm->item->item_name.'-'.$itm->price][] = (int)$itm->platform;
                                }
                            } else {
                                $mItem .= ($mItem == '' ? '(' : '') . $itm->item_id;
                                $mItemArray[] = $itm->item_id;
                            }
                            if ($key1 != count($modifier->items) - 1) {
                                $mItem .= ', ';
                            }
                            $modifierArray[$itm->item->entity_id.'-'.$itm->price] = $itm;
                        } elseif ((!is_null($itm->item) && in_array($itm->item->item_name, $mItemArray))) {
                            if (is_null($itm->platform)) {
                                //$platform[] = $itm->item->delivery_platform_id;
                            } else {
                                $platform[$itm->item->item_name.'-'.$itm->price][] = (int)$itm->platform;
                            }
                            $modifierArray[$itm->item->entity_id.'-'.$itm->price] = $itm;
                        }
                    }
                    foreach ($modifierArray as $mkey => $modArrayItem) {
                        $modArrayItem->delivery_platforms = (isset($platform[$modArrayItem->item->item_name.'-'.$modArrayItem->price])?array_values(array_unique($platform[$modArrayItem->item->item_name.'-'.$modArrayItem->price])):[]);
                        $formatedArray[] = $modArrayItem;
                    }
                    $mItem .= ($mItem == '' ? '' : ')');
                }
                $mItem = str_replace(', )', ')', $mItem);
                $modifierList[] = ['id' => $modifier->id, 'title' => $title, 'modifiers' => $mItem, 'modifier_array' => array_values(array_unique($mItemArray)), 'platform' => (isset($platforms[$modifier->delivery_platform]) ? $platforms[$modifier->delivery_platform] : null), 'modifier_items' => array_values($formatedArray), 'modifier' => $modifier];
            }
            return $this->success('Modifiers', $modifierList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getWizardAllergies($data)
    {
        try {
            $allergies = ["no_allergens", "celery", "crustaceans", "eggs", "fish", "gluten", "lupin", "milk", "molluscs", "mustard", "nuts", "peanuts", "sesame_seeds", "soybeans", "sulphur_dioxide_sulphites"];
            $allergyList = [];
            foreach ($allergies as $key => $allergy) {
                $allergyList[] = ['id' => $allergy, 'value' => ucfirst(str_replace('_', ' ', $allergy))];
            }
            return $this->success('Allergies', $allergyList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getWizardPlatforms($data)
    {
        try {
            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platforms = (json_decode($response->getBody()))->data;
            $platformList = [];
            foreach ($platforms as $key => $platform) {
                $platformList[$platform->id] = ['id' => $platform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $platform->name))), 'logo' => $platform->logo];
            }
            return $this->success('Platforms', $platformList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getMainMenuOutlets($id, $brandId = null)
    {
        try {
            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platforms = (json_decode($response->getBody()))->data;
            $platformList = [];
            foreach ($platforms as $key => $platform) {
                $platformList[$platform->outlet_id][] = ['id' => $platform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $platform->name))), 'logo' => $platform->logo];
            }

            $mainMenu = MainMenu::find($id);
            $mainMenu->outlets = $mainMenu->shops;
            foreach ($mainMenu->shops as $key => $outlet) {
                $outlet->availability = unserialize($outlet->service_availability);
                if (isset($platformList[$outlet->id])) {
                    $outlet->platforms = $platformList[$outlet->id];
                } else {
                    $outlet->platforms = [];
                }
                if (!is_null($brandId)) {
                    $dp = DB::table('delivery_platform')->where('webshop_brand_id', $brandId)->where('outlet_id', $outlet->id)->first();
                    if (!is_null($dp)) {
                        $outlet->selected_menu = $dp->selected_menu;
                    }
                }
            }
            return $this->success('Main menu outlets', $mainMenu);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function cloneMainMenu($id, $data)
    {
        try {
            $mainMenu = MainMenu::find($id);
            if (is_null($mainMenu)) {
                return $this->notFound('Main Menu not found');
            }
            DB::transaction(function () use ($mainMenu, $data) {
                $clonedMainMenu = $mainMenu->replicate();
                $clonedMainMenu->name = $mainMenu->name . ' Copy';
                $clonedMainMenu->save();

                $mainMenuMenus = MainMenuMenu::where('main_menu_id', $mainMenu->id)->get();
                foreach ($mainMenuMenus as $key1 => $menu) {
                    $menuMainMenu = $menu->replicate();
                    $menuMainMenu->main_menu_id = $clonedMainMenu->id;
                    $menuMainMenu->save();
                }

                $categoryMenus = CategoryMenu::where('main_menu_id', $mainMenu->id)->get();
                foreach ($categoryMenus as $key1 => $category) {
                    $categoryMenu = $category->replicate();
                    $categoryMenu->main_menu_id = $clonedMainMenu->id;
                    $categoryMenu->save();
                }

                $itemPrices = ItemPrice::where('main_menu_id', $mainMenu->id)->get();
                foreach ($itemPrices as $key1 => $itemPrice) {
                    $itemPriceMenu = $itemPrice->replicate();
                    $itemPriceMenu->main_menu_id = $clonedMainMenu->id;
                    $itemPriceMenu->save();
                }

                $itemCategories = ItemCategory::where('main_menu_id', $mainMenu->id)->get();
                foreach ($itemCategories as $key1 => $itemCategory) {
                    $itemCategoryMenu = $itemCategory->replicate();
                    $itemCategoryMenu->main_menu_id = $clonedMainMenu->id;
                    $itemCategoryMenu->save();
                }

                $modifierGroups = ModifierGroup::where('main_menu_id', $mainMenu->id)->get();
                foreach ($modifierGroups as $key1 => $modifierGroup) {
                    $newModifier = $modifierGroup->replicate();;
                    $newModifier->main_menu_id = $clonedMainMenu->id;
                    $newModifier->save();

                    $modifierGroupItems = ModifierGroupItem::where('modifier_group_id', $modifierGroup->id)->get();
                    foreach ($modifierGroupItems as $key3 => $modifierGroupItem) {
                        $newModifierGroupItem = $modifierGroupItem->replicate();;
                        $newModifierGroupItem->modifier_group_id = $newModifier->id;
                        $newModifierGroupItem->save();
                    }

                    $modifierGroupModifierItems = ModifierGroupModifierItem::where('modifier_group_id', $modifierGroup->id)->get();
                    foreach ($modifierGroupModifierItems as $key4 => $modifierGroupModifierItem) {
                        $newModifierGroupModifierItem = $modifierGroupModifierItem->replicate();;
                        $newModifierGroupModifierItem->modifier_group_id = $newModifier->id;
                        $newModifierGroupModifierItem->save();
                    }
                }

                if (isset($data['clone_shops']) && $data['clone_shops'] == "true") {
                    $shopMainMenus = ShopMainMenu::where('main_menu_id', $mainMenu->id)->get();
                    foreach ($shopMainMenus as $key1 => $shopMenu) {
                        $shopMainMenu = $shopMenu->replicate();
                        $shopMainMenu->main_menu_id = $clonedMainMenu->id;
                        $shopMainMenu->save();
                    }
                }
            });
            CommonHelper::userLog(null, ['description' => 'Cloned main menu', 'event' => 'clone', 'subject_type' => 'main_menu', 'subject_id' => $mainMenu->id]);
            return $this->success('Successfully duplicated the main menu');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getPreviewCategories($id, $dp = null, $active = false, $query = '', $outlet = null, $masterDp = null, $menuId = null)
    {
        try {
            $mainMenu = MainMenu::find($id);
            if (is_null($mainMenu)) {
                return $this->notFound('Main menu not found');
            }
            if (is_null($menuId)) {
                $allowedMenuIds = MainMenuMenu::where('main_menu_id', $mainMenu->id)->pluck('menu_id')->toArray();
            } else {
                $allowedMenuIds = [$menuId];
            }
            $allowedCategoryIds = CategoryMenu::where('main_menu_id', $mainMenu->id)->whereIn('menu_id', $allowedMenuIds)->pluck('category_id')->toArray();
            if (is_null($menuId)) {
                $itemIds = unserialize($mainMenu->item_ids);
            } else {
                $menu = Menu::find($menuId);
                $itemIds = (is_null($menu->item_ids)?[]:unserialize($menu->item_ids));
            }
            if (!$itemIds) {
                $itemIds = [];
            }
            $categoryOrder = Category::whereIn('id', $allowedCategoryIds)->where('status', 1)->orderBy('priority');
            $bogoCategoryOrder = Category::where('is_bogo_category', 1)->whereNotIn('id', $allowedCategoryIds)->where('status', 1)->get();
            $deliveryPlatform = null;
            if (!is_null($dp)) {
                $deliveryPlatform = DB::table('delivery_platform')->find($dp);
                if (!is_null($deliveryPlatform) && $deliveryPlatform->platform_id == 8) {
                    $categoryOrder = $categoryOrder->where('is_bogo_category', 0);
                    $bogoCategoryOrder = null;
                }
            }
            $categoryOrder = $categoryOrder->pluck('title')->toArray();
            if (in_array('Offers', $categoryOrder)) {
                if (($key = array_search('Offers', $categoryOrder)) !== false) {
                    unset($categoryOrder[$key]);
                }
            }
            $tmpCatName = ['Offers'];
            if (!is_null($bogoCategoryOrder) && count($bogoCategoryOrder->pluck('title')->toArray())>0) {
                $tmpCatName = array_merge($tmpCatName, $bogoCategoryOrder->pluck('title')->toArray());
            }
            $categoryOrder = array_merge($tmpCatName, $categoryOrder);
            $categoryOrder[] = 'Others';

            $modifierGroupList = CommonHelper::getModifierGroups($dp, $mainMenu->id);
            $modifierItemIds = [];
            foreach ($modifierGroupList as $mgk => $modGroup) {
                $modifierItemIds = array_merge($modifierItemIds, collect($modGroup['items'])->pluck('entity_id')->toArray());
            }
            if ($query == '') {
                $items = Item::whereIn('id', array_merge($itemIds, $modifierItemIds))->where('status', 1)->get();
            } else {
                $items = Item::whereIn('id', array_merge($itemIds, $modifierItemIds))->where('title', 'LIKE', '%' . $query . '%')->where('status', 1)->get();
            }
            $categoryItems = [];
            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platforms = (json_decode($response->getBody()))->data;
            $platformList = [];
            $dpIds = [];
            foreach ($platforms as $key => $platform) {
                $platformList[$platform->id] = ['id' => $platform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $platform->name))), 'logo' => $platform->logo];
                if (!is_null($outlet) && ($platform->outlet_id==$outlet)) {
                    $dpIds[] = $platform->id;
                }
            }
            $activeModifierItemIds = [];
            $tmpModifierItemPrices = [];
            $activeModifierItemPrices = [];

            $shopName = null;
            $brandDomain = null;
            if (!is_null($outlet)) {
                $shop = Shop::find($outlet);
                if (!is_null($shop)) {
                    $shopName = $shop->name;
                }
            }
            if (!is_null($deliveryPlatform)) {
                $brandId = $deliveryPlatform->webshop_brand_id;
                if (!is_null($brandId)) {
                    $brand = DB::table('webshop_brand')->find($brandId);
                    if (!is_null($brand)) {
                        $brandDomain = $brand->domain;
                    }
                }
                if (is_null($shopName)) {
                    $shopId = $deliveryPlatform->outlet_id;
                    if (!is_null($shopId)) {
                        $shop = Shop::find($shopId);
                        if (!is_null($shop)) {
                            $shopName = $shop->name;
                        }
                    }
                }
            }

            foreach ($items as $key => $item) {
                $item->availability = 0;
                $item->snooze_available = 1;
                $item->modifiers = [];
                $item->images = $item->images;
                $item->printer_groups = $item->printerGroups->map(function ($printerGroup) {
                    unset($printerGroup->brand_id);
                    unset($printerGroup->shop_id);
                    unset($printerGroup->created_at);
                    unset($printerGroup->updated_at);
                    unset($printerGroup->pivot);
                    return $printerGroup;
                });
                if (is_null($dp)) {
                    $item->priceList = $item->prices->where('main_menu_id', $id);
                    if (!is_null($outlet)) {
                        $commonDPIds = array_intersect($dpIds, $item->priceList->pluck('delivery_platform_id')->toArray());
                        $item->entityDeliveryItem = $item->entityDeliveryPlatform->whereIn('delivery_platform_id', $dpIds);
                    } else {
                        $item->entityDeliveryItem = $item->entityDeliveryPlatform;
                    }
                } else {
                    $item->priceList = $item->prices->where('main_menu_id', $id)->where('delivery_platform_id', $dp);
                    $item->entityDeliveryItem = $item->entityDeliveryPlatform->where('delivery_platform_id', $dp);
                    if (count($item->priceList) > 0) {
                        $item->availability = 1;
                    }
                    if (count($item->entityDeliveryItem)>0) {
                        $entityItem  = $item->entityDeliveryItem->first();
                        $item->availability = $entityItem->available;
                        //$item->modifiers = $entityItem->modifierList($dp);
                        $addedModList = $entityItem->modifiers->pluck('modifier_group_id')->toArray();
                        $modList = array_intersect_key($modifierGroupList, array_flip($addedModList));
                        $modList = array_values($modList);

                        $unselectedModifierIds = array_diff($addedModList, array_keys($modifierGroupList));
                        $unselectedModifiers = ModifierGroup::whereIn('id', $unselectedModifierIds)->where('main_menu_id', $mainMenu->id)->where('status', 1)->get();
                        if (count($unselectedModifiers)>0) {
                            foreach ($unselectedModifiers as $keyUnMod => $unslctMod) {
                                if ($unslctMod->min_permitted>0 && $unslctMod->min_permitted==$unslctMod->max_permitted && count(ModifierGroupModifierItem::where('modifier_group_id', $unslctMod->id)->where('platform', $dp)->get())>0) {
                                    $item->availability = 0;
                                }
                            }
                        }

                        $item->modifiers = $modList;
                        foreach ($modList as $mlkey => $modListElement) {
                            $activeModifierItemIds = array_merge($activeModifierItemIds, collect($modListElement['items'])->pluck('entity_id')->toArray());
                            $tmpModifierItemPrices = array_merge($tmpModifierItemPrices, collect($modListElement['items'])->pluck('price')->toArray());
                            if (count($activeModifierItemIds) == count($tmpModifierItemPrices)) {
                                $activeModifierItemPrices = array_combine($activeModifierItemIds, $tmpModifierItemPrices);
                            }
                        }
                    }
                }
                foreach ($item->entityDeliveryItem as $enkey => $entityItem) {
                    if (isset($platformList[$entityItem->delivery_platform_id])) {
                        $entityItem->platform = $platformList[$entityItem->delivery_platform_id];
                    } else {
                        $entityItem->platform = [];
                    }
                }
                if (count($item->entityDeliveryItem) > 0) {
                    $item->snooze_available = $item->entityDeliveryItem->first()->available;
                    if (is_null($masterDp)) {
                        $tmpAllergies = $item->entityDeliveryItem->first();
                        $item->allergies = (is_null($tmpAllergies)?[]:unserialize($tmpAllergies->allergies));
                    } else {
                        $tmpAllergies = $item->entityDeliveryPlatform->where('delivery_platform_id', $masterDp)->first();
                        $item->allergies = (is_null($tmpAllergies)?[]:unserialize($tmpAllergies->allergies));
                    }
                    $allergies = [];
                    foreach ($item->allergies as $alkey => $allergy) {
                        $allergies[] = ucfirst(str_replace('_', ' ', $allergy));
                    }
                    $item->allergies = $allergies;
                }
                $item->sale_price = 0;
                $item->is_sale = 0;
                $item->has_bogo_offer = 0;
                $item->bogo_category = null;
                $item->bogo_category_name = null;
                $item->bogo_buy_quantity = 0;
                $item->bogo_get_quantity = 0;
                $bogoCategory = null;
                if (count($item->priceList) > 0) {
                    $item->price = $item->priceList->first()->price;
                    $item->sale_price = $item->priceList->first()->sale_price;
                    $item->is_sale = $item->priceList->first()->is_sale;
                    $item->has_bogo_offer = $item->priceList->first()->has_bogo_offer;
                    if ($item->has_bogo_offer && !is_null($item->priceList->first()->category) && $item->priceList->first()->category->is_bogo_category && $item->priceList->first()->category->status) {
                        $bogoCategory = $item->priceList->first()->category;
                        $item->bogo_category = $bogoCategory->id;
                        $item->bogo_category_name = $bogoCategory->title;
                        $item->bogo_buy_quantity = $item->priceList->first()->category->buy_quantity;
                        $item->bogo_get_quantity = $item->priceList->first()->category->get_quantity;
                    }
                }
                unset($item->prices);
                $item->item_category_id = null;
                $categories = $item->categoriesByMainMenu($id);

                $categoryName = null;
                if ((in_array($item->id, $modifierItemIds) && !in_array($item->id, $itemIds)) || ((!is_null($deliveryPlatform) && $deliveryPlatform->platform_id == 9 ? $item->price >= 0 : $item->price > 0) && (is_null($dp) || (count($item->entityDeliveryItem)>0 && count($item->priceList)>0)))) {
                    if (count($categories) > 0 & in_array($item->id, $itemIds)) {
                        if (!$active || ($active && $item->availability == 1 && in_array($categories->first()->id, $allowedCategoryIds))) {
                            $item->item_category_id = $categories->first()->id;
                            $categoryItems[$categories->first()->title][] = $item;
                            $categoryName = $categories->first()->title;

                            if (!is_null($bogoCategory) && $item->has_bogo_offer && (is_null($deliveryPlatform) || (!is_null($deliveryPlatform) && $deliveryPlatform->platform_id != 8))) {
                                $categoryItems[$bogoCategory->title][] = $item;
                                $categoryName = $bogoCategory->title;
                            }

                            if ($item->is_sale && $categories->first()->title!='Offers') {
                                $categoryItems['Offers'][] = $item;
                                $categoryName = 'Offers';
                            }
                        }
                    } else {
                        if (!$active || ($active && $item->availability == 1)) {
                            $categoryItems['Others'][] = $item;
                            $categoryName = 'Others';
                        }
                    }
                }
                // Add item_url property
                if (!is_null($brandDomain) && !is_null($shopName) && !is_null($categoryName)) {
                    $item->item_url = 'https://' . $brandDomain . '/' . rawurlencode($shopName) . '/food-menu?category=' . rawurlencode($categoryName) . '&item=' . rawurlencode($item->title);
                }
            }
            if(isset($categoryItems['Others'])) {
                $otherItems = [];
                foreach ($categoryItems['Others'] as $OIkey => $otherItem) {
                    if (in_array($otherItem->id, $activeModifierItemIds)) {
                        if (array_key_exists($otherItem->id, $activeModifierItemPrices)) {
                            $otherItem->price = $activeModifierItemPrices[$otherItem->id];
                        }
                        $otherItems[] = $otherItem;
                    }
                }
                $categoryItems['Others'] = $otherItems;
            }

            uksort($categoryItems, function ($key1, $key2) use ($categoryOrder) {
                return ((array_search($key1, $categoryOrder) > array_search($key2, $categoryOrder)) ? 1 : -1);
            });

            return $this->success('Categories', $categoryItems);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getPreviewItems($id, $dp = null, $active = false, $query = '', $outlet = null, $includeModifiers = null)
    {
        try {
            $mainMenu = MainMenu::find($id);
            if (is_null($mainMenu)) {
                return $this->notFound('Main menu not found');
            }
            $itemIds = unserialize($mainMenu->item_ids);
            if (!$itemIds) {
                $itemIds = [];
            }
            if ($includeModifiers) {
                $platformIds = unserialize($mainMenu->platform_ids);
                $modifierGroupItemIds = ModifierGroupModifierItem::whereIn('platform', $platformIds)->pluck('item_id')->unique()->toArray();
                $entityItemIds = EntityDeliveryPlatform::whereIn('external_item_id', $modifierGroupItemIds)->pluck('entity_id')->unique()->toArray();
                $itemIds = array_merge($itemIds, $entityItemIds);
            }
            if ($query == '') {
                $items = Item::whereIn('id', $itemIds)->where('status', 1)->orderBy('title', 'ASC')->get();
            } else {
                $items = Item::whereIn('id', $itemIds)->where('title', 'LIKE', '%' . $query . '%')->where('status', 1)->orderBy('title', 'ASC')->get();
            }
            $itemList = ['Snoozed' => [], 'Available' => []];

            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platforms = (json_decode($response->getBody()))->data;
            $platformList = [];
            $dpIds = [];
            foreach ($platforms as $key => $platform) {
                $platformList[$platform->id] = ['id' => $platform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $platform->name))), 'logo' => $platform->logo];
                if (!is_null($outlet) && ($platform->outlet_id==$outlet)) {
                    $dpIds[] = $platform->id;
                }
            }
            foreach ($items as $key => $item) {
                $item->availability = 0;
                $item->snooze_available = 1;

                if (is_null($dp)) {
                    $item->priceList = $item->prices->where('main_menu_id', $id);
                    if (!is_null($outlet)) {
                        if ($includeModifiers) {
                            $item->entityDeliveryItem = $item->entityDeliveryPlatform;
                        } else {
                            $commonDPIds = array_intersect($dpIds, $item->priceList->pluck('delivery_platform_id')->toArray());
                            $item->entityDeliveryItem = $item->entityDeliveryPlatform->whereIn('delivery_platform_id', $commonDPIds);
                        }
                        $entityDeliveryItemArray = [];
                        $addedEntityDeliveryItemArray = [];
                        foreach ($item->entityDeliveryItem as $entKey => $enDelItm) {
                            if (!in_array($enDelItm->delivery_platform_id, $addedEntityDeliveryItemArray)) {
                                $entityDeliveryItemArray[] = $enDelItm;
                                $addedEntityDeliveryItemArray[] = $enDelItm->delivery_platform_id;
                            }
                        }
                        $item->entityDeliveryItem = collect($entityDeliveryItemArray);
                    } else {
                        $item->entityDeliveryItem = $item->entityDeliveryPlatform;
                    }
                } else {
                    $item->priceList = $item->prices->where('main_menu_id', $id)->where('delivery_platform_id', $dp);
                    $item->entityDeliveryItem = $item->entityDeliveryPlatform->where('delivery_platform_id', $dp);
                    if (count($item->priceList) > 0) {
                        $item->availability = 1;
                    }
                }
                foreach ($item->entityDeliveryItem as $enkey => $entityItem) {
                    if (isset($platformList[$entityItem->delivery_platform_id])) {
                        $entityItem->platform = $platformList[$entityItem->delivery_platform_id];
                    } else {
                        $entityItem->platform = [];
                    }
                }
                if (count($item->entityDeliveryItem) > 0) {
                    $item->snooze_available = $item->entityDeliveryItem->first()->available;
                }
                if (count($item->priceList) > 0) {
                    $item->price = $item->priceList->first()->price;
                }
                $item->has_price_variation = 0;
                if ($item->priceList->min('price') != $item->priceList->max('price')) {
                    $item->price = $item->priceList->min('price');
                    $item->has_price_variation = 1;
                }

                if (($active && count($item->entityDeliveryItem)>0 && (count($item->priceList)>0 || $includeModifiers)) || !$active) {
                    $tmpEntityItems = [];
                    foreach ($item->entityDeliveryItem as $keyEn => $itm) {
                        $tmpEntityItems[] = $itm;
                    }
                    $item->entityDeliveryItem = $tmpEntityItems;
                    if (count($item->categories)>0 || $includeModifiers) {
                        if (count($item->categories)==0 && count($item->entityDeliveryItem)>0 && CommonHelper::isModifier($item->entityDeliveryItem[0]->external_item_id)) {
                            $item->price = '0.00';
                        }
                        if ($item->snooze_available == 0) {
                            $itemList['Snoozed'][] = $item;
                        } else {
                            $itemList['Available'][] = $item;
                        }
                    }
                }
            }
            return $this->success('Items', $itemList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getCategoriesWhichHasItems($id)
    {
        try {
            $mainMenu = MainMenu::find($id);
            $categoryOrder = $mainMenu->categories;
            $categoryList = [];
            $categoryIds = [];
            foreach ($categoryOrder as $key => $category) {
                if (!in_array($category->id, $categoryIds)) {
                    $categoryIds[] = $category->id;
                    $categoryList[] = $category;
                }
            }
            return $this->success('Categories', $categoryList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function updateMainMenuStatus()
    {
        try {
            $mainMenus = MainMenu::with('shops:id')
                ->whereIn('id', DB::table('delivery_platform')->pluck('selected_menu'))
                ->get();
            MainMenu::whereIn('id', $mainMenus->pluck('id'))->update(['status' => 'ACTIVE']);
            $menuIds = $mainMenus->pluck('id')->toArray();
            CommonHelper::userLog(null, ['description' => 'Updated main menu status', 'event' => 'update', 'subject_type' => 'main_menu', 'subject_id' => implode(',', $menuIds)]);
            $shopIds = $mainMenus->pluck('shops.*.id')->flatten()->unique()->toArray();
            // UpdateSnoozeItemList::dispatch(['shopIds' => $shopIds, 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
            return $this->success('Successfully updated');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function searchInMainMenu($id, $query, $outlet, $dp)
    {
        if ((is_null($id) || $id=='null') && !is_null($dp)) {
            $deliveryPlatform = DB::table('delivery_platform')->find($dp);
            if (!is_null($deliveryPlatform)) {
                $id = $deliveryPlatform->selected_menu;
            }
        }
        $items = $this->getPreviewItems($id, $dp, false, $query, $outlet, true);
        return $items;
    }

    public function deleteOutletFromMainMenu($id, $outlet)
    {
        try {
            DB::transaction(function () use ($id, $outlet) {
                ShopMainMenu::where('shop_id', $outlet)->where('main_menu_id', $id)->delete();
            });
            CommonHelper::userLog(null, ['description' => 'Deleted outlet from main menu', 'event' => 'delete', 'subject_type' => 'main_menu', 'subject_id' => $id]);
            return $this->success('Successfully deleted');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getEntityItems($id)
    {
        try {
            $mainMenu = MainMenu::find($id);
            $itemIds = unserialize($mainMenu->item_ids);
            if ($itemIds == false) {
                $itemIds = [];
            }
            $entityItems = EntityDeliveryPlatform::/*whereIn('entity_id', $itemIds)->*/whereNotNull('entity_id')->orderBy('entity_id')->get();
            $tmpIds = [];
            $items = [];
            foreach ($entityItems as $key => $item) {
                if (!in_array($item->entity_id, $tmpIds)) {
                    $items[] = $item;
                    $tmpIds[] = $item->entity_id;
                }
            }

            return $this->success('Entity items', $items);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function updateStatus($id, $status)
    {
        try {
            $mainMenu = MainMenu::find($id);
            $mainMenu->status = $status;
            $mainMenu->save();
            CommonHelper::userLog(null, ['description' => 'Updated main menu status', 'event' => 'update', 'subject_type' => 'main_menu', 'subject_id' => $id]);
            return $this->success('Updated main menu status');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getCategoryByShopId($brandId, $shopId, $type)
    {
        try {
            $dp = DB::table('delivery_platform')->where('outlet_id', $shopId)->where('platform_id', ($type == 'TABLE_ORDER'?8:6))->where('webshop_brand_id', $brandId)->where('status', 'active')->get()->first();
            if (is_null($dp)) {
                return $this->notFound('Delivery platform not found', []);
            }
            $shop = Shop::find($shopId);

            $day =  strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Now', 'l', $shop->timezone));
            $time =  strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Now', 'H:i:s', $shop->timezone));

            $mainMenu = MainMenu::find($dp->selected_menu);
            $webshopMenu = [];
            if (!is_null($mainMenu)) {
                $submenuIds = $mainMenu->menus->pluck('id')->toArray();

                // Build base query conditions
                $baseConditions = [
                    'main_menu_id' => $dp->selected_menu,
                    'delivery_platform_id' => $dp->id,
                    'status' => 1,
                    'outlet_id' => $shopId
                ];

                $webshopMenu = $this->findWebshopMenu($baseConditions, $submenuIds, $day, $time, $shop->timezone);

                // $webshopMenu = WebshopMenu::where('main_menu_id', $dp->selected_menu)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp->id)->where('status', 1)->where('outlet_id', $shopId)->where('day', $day)->whereTime('from', '<=', $time)->whereTime('to', '>=', $time)->orderBy('id', 'DESC')->get();
                // if (count($webshopMenu)==0) {
                //     $previousDay = strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Yesterday', 'l', $shop->timezone));
                //     $webshopMenu = WebshopMenu::where('main_menu_id', $dp->selected_menu)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp->id)->where('status', 1)->where('outlet_id', $shopId)->where('day', $previousDay)/*->whereTime('from', '<=', $time)*/->whereTime('to', '>=', $time)->orderBy('id', 'DESC')->get();
                //     if (count($webshopMenu)==0 || ($webshopMenu->first()->from < $webshopMenu->first()->to)) {
                //         $webshopMenu = WebshopMenu::where('main_menu_id', $dp->selected_menu)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp->id)->where('status', 1)->where('outlet_id', $shopId)/*->where('day', $day)->whereTime('to', '>=', $time)*/->orderBy('id', 'DESC')->get();
                //         //$webshopMenu = WebshopMenu::where('main_menu_id', $dp->selected_menu)->where('delivery_platform_id', $dp->id)->where('status', 1)->where('outlet_id', $shopId)->where('day', $day)->whereTime('to', '>=', $time)->orderBy('id', 'DESC')->get();
                //     }
                // }
            }
            if (count($webshopMenu)>0) {
                $webshopMenu = $webshopMenu->first();
                $categoryIds = unserialize($webshopMenu->category_ids);
                $categories = DB::table('category')->whereIn('id', $categoryIds)->orderBy('priority', 'ASC')->selectRaw('category.title, category.sub_title, category.description, category.id, category.priority')->orderBy('category.priority', 'ASC')->get();
                if (count($categories->where('title', 'Offers'))==0) {
                    $categoryList = unserialize($webshopMenu->menu);
                    $catKeys = array_keys($categoryList);
                    if (in_array('Offers', $catKeys)) {
                        $categories = $categories->toArray();
                        $offerArray[] = (object)['title' => 'Offers', 'sub_title' => null, 'description' => 'Offers', 'id' => 1000000, 'priority' => 0];
                        $categories = array_merge($offerArray, $categories);
                        $categories = collect($categories);
                    }
                }
                return $this->success('Category By Shop ID', ['categories'=> $categories]);
            }

            return $this->success('Category By Shop ID', ['categories'=> []]);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error($e->getMessage());
        }
    }

    public function findWebshopMenu($baseConditions, $submenuIds, $day, $time, $timezone)
    {
        $currentDayMenu = $this->findMenuForDay($baseConditions, $submenuIds, $day, $time);
        if ($currentDayMenu->isNotEmpty()) {
            return $currentDayMenu;
        }

        $futureDayMenu = $this->findNextAvailableMenu($baseConditions, $submenuIds, $day, $time, $timezone);
        if ($futureDayMenu->isNotEmpty()) {
            return $futureDayMenu;
        }
        return collect();
    }

    public function findMenuForDay($baseConditions, $submenuIds, $day, $time)
    {
        $query = WebshopMenu::where($baseConditions)
            ->whereIn('submenu_id', $submenuIds)
            ->where('day', $day);

        if ($query->count() === 0) {
            return collect();
        }

        $currentTimeMenu = (clone $query)
            ->whereTime('from', '<=', $time)
            ->whereTime('to', '>=', $time)
            ->orderBy('id', 'DESC')
            ->limit(1)
            ->get();

        if ($currentTimeMenu->isNotEmpty()) {
            return $currentTimeMenu;
        }

        return (clone $query)
            ->whereTime('from', '>', $time)
            ->orderBy('from', 'ASC')
            ->orderBy('id', 'DESC')
            ->limit(1)
            ->get();
    }

    public function findNextAvailableMenu($baseConditions, $submenuIds, $currentDay, $currentTime, $timezone)
    {
        $futureDays = [];
        for ($i = 1; $i < 7; $i++) {
            $futureDays[] = strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('+' . $i . ' day', 'l', $timezone));
        }

        $allDays = array_merge($futureDays, [$currentDay]);

        return WebshopMenu::where($baseConditions)
            ->whereIn('submenu_id', $submenuIds)
            ->whereIn('day', $allDays)
            ->orderByRaw("
                CASE
                    WHEN day = ? THEN 1
                    WHEN day = ? THEN 2
                    WHEN day = ? THEN 3
                    WHEN day = ? THEN 4
                    WHEN day = ? THEN 5
                    WHEN day = ? THEN 6
                    WHEN day = ? THEN 7
                    ELSE 8
                END
            ", $allDays)
            ->orderBy('from', 'ASC')
            ->orderBy('id', 'DESC')
            ->limit(1)
            ->get();
    }

    public function getWebshopCategoryItemsByMenuIdAndShopId($id, $dp = null, $active = true, $query = '', $outlet = null)
    {
        $menu = [];
        $shop = Shop::find($outlet);
        $day =  strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Now', 'l', $shop->timezone));
        $time =  DateTimeUtility::getDateTimeFormatWithTimeZone('Now', 'H:i:s', $shop->timezone);
        $mainMenu = MainMenu::find($id);

        $deliveryPlatform = DB::table('delivery_platform')->find($dp);
        $webshopMenu = [];
        if (!is_null($mainMenu)) {
            $submenuIds = $mainMenu->menus->pluck('id')->toArray();

            // Build base query conditions
            $baseConditions = [
                'main_menu_id' => $id,
                'delivery_platform_id' => $dp,
                'status' => $active,
                'outlet_id' => $outlet
            ];

            $webshopMenu = $this->findWebshopMenu($baseConditions, $submenuIds, $day, $time, $shop->timezone);

            // $webshopMenu = WebshopMenu::where('main_menu_id', $id)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp)->where('status', $active)->where('outlet_id', $outlet)->where('day', $day)->whereTime('from', '<=', $time)->whereTime('to', '>=', $time)->orderBy('id', 'DESC')->get();

            // if (count($webshopMenu)==0) {
            //     $previousDay = strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Yesterday', 'l', $shop->timezone));
            //     $webshopMenu = WebshopMenu::where('main_menu_id', $id)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp)->where('status', $active)->where('outlet_id', $outlet)->where('day', $previousDay)/*->whereTime('from', '>=', $time)*/->whereTime('to', '>=', $time)->orderBy('id', 'DESC')->get();
            //     if (count($webshopMenu)==0 || ($webshopMenu->first()->from < $webshopMenu->first()->to)) {
            //         $webshopMenu = WebshopMenu::where('main_menu_id', $id)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp)->where('status', $active)->where('outlet_id', $outlet)/*->where('day', $day)->whereTime('to', '>=', $time)*/->orderBy('id', 'DESC')->get();
            //     }
            // }
        }
        if (count($webshopMenu)>0) {
            $webshopMenu = $webshopMenu->first();
            $menu = unserialize($webshopMenu->menu);
            return $this->success('Categories', $menu);
        }

        return $this->success('Categories', $menu);
    }

    public function getWebshopItemsByMenuIdAndShopId($id, $dp = null, $active = true, $query = '', $outlet = null)
    {
        $result = $this->getPreviewCategories($id, $dp, $active, $query, $outlet);
        if ($result->getStatusCode()!=200) {
            return $result;
        }
        $result = (array)(json_decode($result->getContent())->data);
        unset($result["Others"]);
        $products = [];
        $addedIds = [];
        foreach ($result as $key => $categoryProduct) {
            foreach ($categoryProduct as $key1 => $product) {
                if (!in_array($product->id, $addedIds)) {
                    $discount = ['amount' => 0, 'discount_type' => 'percentage'];
                    $priceList = collect($product->priceList);
                    if (count((array)$product->priceList)>0) {
                        $pricelist = $priceList->first();
                        $discount = ['amount' => $pricelist->discount_amount, 'discount_type' => $pricelist->discount_type];
                    }
                    $product->discount = $discount;
                    $products[] = $product;
                    $addedIds[] = $product->id;
                }
            }
        }
        $dp = DB::table('delivery_platform')->find($dp);
        if ($dp->is_master == 0) {
            $integration_type = 'Slave';
        } else {
            $integration_type = 'Master';
        }
        $data = [
            'products' => $products,
            'integration_type' => $integration_type,
        ];
        return $this->success('Products', $data);
    }

    public function updateWebshopMenu($id, $dp = null, $active = true, $query = '', $outlet = null, $batch = null, $skipSnoozeUpdate = false)
    {
        if (is_null($batch)) {
            $batch = time();
        }
        $mainMenu = MainMenu::find($id);
        $result = [];
        if (count($mainMenu->menus->where('main_menu_id', $mainMenu->id))==0) {
            $this->request_handle_service->postRequst(['from' => ucfirst(CommonHelper::getXTenantCode($_SERVER)), 'message' => 'We tried to publish the Menu which does not contain any sub menus. Please inform the Admin. Tenant code:'.CommonHelper::getXTenantCode($_SERVER).', Main menu: '. $mainMenu->name], 'api/v1/send-slack-message', 'notification_service', CommonHelper::getXTenantCode($_SERVER));
            return $this->error('Menu does not contain any sub menus.');
        }
        $shopMenu = null;
        $shop = Shop::find($outlet);
        if (count($mainMenu->menus->where('main_menu_id', $mainMenu->id))==1) {
            if (!is_null($shop->service_availability)) {
                $shopMenu = $shop->service_availability;
            }
        }
        $deliveryPlatform = DB::table('delivery_platform')->find($dp);
        foreach ($mainMenu->menus->where('main_menu_id', $mainMenu->id) as $key => $menu) {
            $result = $this->getPreviewCategories($id, $dp, $active, $query, $outlet, null, $menu->id);
            if ($result->getStatusCode()!=200) {
                return $result;
            }
            $result = (array)(json_decode($result->getContent())->data);
            unset($result["Others"]);
            if (count($result)==0) {
                $this->request_handle_service->postRequst(['from' => ucfirst(CommonHelper::getXTenantCode($_SERVER)), 'message' => 'We tried to publish the Menu which does not contain any item. Please inform the Admin. Tenant code:'.CommonHelper::getXTenantCode($_SERVER).', Main menu: '. $mainMenu->name.', Menu: '.$menu->title], 'api/v1/send-slack-message', 'notification_service', CommonHelper::getXTenantCode($_SERVER));
                return $this->error('Menu does not contain any item.');
            }
            $categoryIds = Category::whereIn('id', $menu->categories->pluck('id')->toArray())->whereIn('title', array_keys($result))->where('status', 1)->orderBy('priority')->pluck('id')->toArray();
            $bogoCategoryOrder = Category::where('is_bogo_category', 1)->whereNotIn('id', $menu->categories->pluck('id')->toArray())->whereIn('title', array_keys($result))->where('status', 1)->get();
            if (count($bogoCategoryOrder->pluck('id')->toArray())>0) {
                $categoryIds = array_merge($categoryIds, $bogoCategoryOrder->pluck('id')->toArray());
            }
            $firstIteration = true;

            $availability = unserialize(is_null($shopMenu)?$menu->service_availability:$shopMenu);
            foreach ($availability as $dayAv => $dayAvailability) {
                if ($dayAvailability['availability']) {
                    foreach ($dayAvailability['time_periods'] as $timeAv => $timeAvailability) {
                        $platformMenu = WebshopMenu::firstOrNew([
                            'main_menu_id' => $id,
                            'delivery_platform_id' => $dp,
                            'platform_id' => $deliveryPlatform->platform_id,
                            'status' => $active,
                            'outlet_id' => $outlet,
                            'day' => $dayAvailability['day_of_week'],
                            'submenu_id' => $menu->id,
                        ]);

                        $platformMenu->menu = serialize($result);
                        $platformMenu->category_ids = serialize($categoryIds);
                        $platformMenu->from = $timeAvailability['start_time'];
                        $platformMenu->to = $timeAvailability['end_time'];
                        $platformMenu->save();

                        $this->copyWebshopMenuToSlave($platformMenu->id, $firstIteration, $deliveryPlatform->platform_id);

                        $firstIteration = false;
                        $this->logMenuHistory($batch, $id, null, $outlet, [], $result, (is_null($menu->item_ids)?[]:unserialize($menu->item_ids)), $dp, $menu->id, $dayAvailability['day_of_week'], $categoryIds);
                    }
                }
            }
        }
        DB::table('delivery_platform')
            ->where('id', $deliveryPlatform->id)
            ->update(['selected_menu' => $id]);
        $menuType = (($deliveryPlatform->platform_id == 8) ? 'Table order' : (($deliveryPlatform->platform_id == 6) ? 'Webshop' : 'DG POS'));
        \Log::info($menuType . ' menu updated' . '. - '.CommonHelper::getXTenantCode($_SERVER));
        CommonHelper::userLog(null, ['description' => 'Updated ' . $menuType . ' menu', 'event' => 'update', 'subject_type' => 'main_menu', 'subject_id' => $id]);
        if (!$skipSnoozeUpdate) {
            $shopIds = $mainMenu->shops->pluck('id')->toArray();
            UpdateSnoozeItemList::dispatch(['shopIds' => $shopIds, 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
        }
        return $this->success($menuType . ' menu updated', $result);
    }

    public function copyWebshopMenuToSlave($masterMenu, $firstIteration = true, $platformId)
    {
        $platformMaster = WebshopMenu::find($masterMenu);
        $slaveDps = DB::table('delivery_platform')->where('parent_platform', $platformMaster->delivery_platform_id)->whereIn('outlet_id', $platformMaster->mainMenu->shops->pluck('id')->toArray())->where('platform_id', $platformId)->whereIn('status', ['active', 'Active'])->get();
        foreach ($slaveDps as $key => $slave) {
            if ($firstIteration) {
                ItemPrice::where('main_menu_id', $platformMaster->main_menu_id)->where('delivery_platform_id', $slave->id)->delete();
                $itemPrices = ItemPrice::where('main_menu_id', $platformMaster->main_menu_id)->where('delivery_platform_id', $platformMaster->delivery_platform_id)->get();
                foreach ($itemPrices as $key1 => $itemPrice) {
                    $itemPriceMenu = $itemPrice->replicate();
                    $itemPriceMenu->delivery_platform_id = $slave->id;
                    $itemPriceMenu->save();
                }

                //EntityDeliveryPlatform::where('delivery_platform_id', $slave->id)->delete();
                $entityItems = EntityDeliveryPlatform::where('delivery_platform_id', $platformMaster->delivery_platform_id)->get();
                foreach ($entityItems as $key1 => $entityItem) {
                    $existingEntity = EntityDeliveryPlatform::where('delivery_platform_id', $slave->id)->where('entity_id', $entityItem->entity_id)->get();
                    if (count($existingEntity)==0) {
                        $entityItemNew = $entityItem->replicate();
                        if (str_contains($entityItem->plu, 'MOD-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                            $entityItemNew->plu = str_replace('MOD-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT), 'MOD-'.str_pad($slave->id, 3, '0', STR_PAD_LEFT), $entityItem->plu);
                        }
                        if (str_contains($entityItem->external_item_id, 'MOD-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                            $entityItemNew->external_item_id = str_replace('MOD-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT), 'MOD-'.str_pad($slave->id, 3, '0', STR_PAD_LEFT), $entityItem->external_item_id);
                        }
                        if (str_contains($entityItem->plu, 'LM-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                            $entityItemNew->plu = str_replace('LM-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT), 'LM-'.str_pad($slave->id, 3, '0', STR_PAD_LEFT), $entityItem->plu);
                        }
                        if (str_contains($entityItem->external_item_id, 'LM-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                            $entityItemNew->external_item_id = str_replace('LM-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT), 'LM-'.str_pad($slave->id, 3, '0', STR_PAD_LEFT), $entityItem->external_item_id);
                        }
                        $entityItemNew->delivery_platform_id = $slave->id;
                        $entityItemNew->save();
                    }
                }

                //ModifierGroupModifierItem::where('platform', $slave->id)->delete();
                $modifierGroupModifierItems = ModifierGroupModifierItem::where('platform', $platformMaster->delivery_platform_id)->get();
                foreach ($modifierGroupModifierItems as $key1 => $modifierGroupModifierItem) {
                    $tmpItemId = $modifierGroupModifierItem->item_id;
                    if (str_contains($modifierGroupModifierItem->item_id, 'MOD-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                        $tmpItemId = str_replace('MOD-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT), 'MOD-'.str_pad($slave->id, 3, '0', STR_PAD_LEFT), $modifierGroupModifierItem->item_id);
                    }
                    if (str_contains($modifierGroupModifierItem->item_id, 'LM-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                        $tmpItemId = str_replace('LM-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT), 'LM-'.str_pad($slave->id, 3, '0', STR_PAD_LEFT), $modifierGroupModifierItem->item_id);
                    }

                    $existingmodifierGroupModifierItem = ModifierGroupModifierItem::where('platform', $slave->id)->where('item_id', $tmpItemId)->get();
                    if (count($existingmodifierGroupModifierItem)==0) {
                        $modifierGroupModifierItemNew = $modifierGroupModifierItem->replicate();
                        if (str_contains($modifierGroupModifierItem->item_id, 'MOD-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                            $modifierGroupModifierItemNew->item_id = $tmpItemId;
                        }
                        if (str_contains($modifierGroupModifierItem->item_id, 'LM-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                            $modifierGroupModifierItemNew->item_id = $tmpItemId;
                        }
                        $modifierGroupModifierItemNew->platform = $slave->id;
                        $modifierGroupModifierItemNew->save();
                    }
                }

                //ModifierGroupItem::where('platform', $slave->id)->delete();
                $modifierGroupItems = ModifierGroupItem::where('platform', $platformMaster->delivery_platform_id)->get();
                foreach ($modifierGroupItems as $key1 => $modifierGroupItem) {
                    $tmpItemId = $modifierGroupItem->item_id;
                    if (str_contains($modifierGroupItem->item_id, 'MOD-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                        $tmpItemId = str_replace('MOD-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT), 'MOD-'.str_pad($slave->id, 3, '0', STR_PAD_LEFT), $modifierGroupItem->item_id);
                    }
                    if (str_contains($modifierGroupItem->item_id, 'LM-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                        $tmpItemId = str_replace('LM-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT), 'LM-'.str_pad($slave->id, 3, '0', STR_PAD_LEFT), $modifierGroupItem->item_id);
                    }
                    $existingmodifierGroupItem = ModifierGroupItem::where('platform', $slave->id)->where('item_id', $tmpItemId)->get();
                    if (count($existingmodifierGroupItem)==0) {
                        $modifierGroupItemNew = $modifierGroupItem->replicate();
                        if (str_contains($modifierGroupItem->item_id, 'MOD-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                            $modifierGroupItemNew->item_id = $tmpItemId;
                        }
                        if (str_contains($modifierGroupItem->item_id, 'LM-'.str_pad($platformMaster->delivery_platform_id, 3, '0', STR_PAD_LEFT))) {
                            $modifierGroupItemNew->item_id = $tmpItemId;
                        }
                        $modifierGroupItemNew->platform = $slave->id;
                        $modifierGroupItemNew->save();
                    }
                }
            }

            $result = $this->getPreviewCategories($platformMaster->main_menu_id, $slave->id, $platformMaster->status, '', $slave->outlet_id, $platformMaster->delivery_platform_id, $platformMaster->submenu_id);
            if ($result->getStatusCode()==200) {
                $result = (array)(json_decode($result->getContent())->data);
                unset($result["Others"]);

                $categoryIds = Category::whereIn('title', array_keys($result))->where('status', 1)->orderBy('priority')->pluck('id')->toArray();

                $mainMenu = MainMenu::find($platformMaster->main_menu_id);
                if (count($mainMenu->menus->where('main_menu_id', $mainMenu->id))==1) {
                    $shop = Shop::find($slave->outlet_id);
                    if (!is_null($shop->service_availability)) {
                        $availability = unserialize($shop->service_availability);
                        foreach ($availability as $dayAv => $dayAvailability) {
                            if ($dayAvailability['availability'] && $dayAvailability['day_of_week']==$platformMaster->day) {
                                foreach ($dayAvailability['time_periods'] as $timeAv => $timeAvailability) {
                                    $platformMaster->from = $timeAvailability['start_time'];
                                    $platformMaster->to = $timeAvailability['end_time'];
                                }
                            }
                        }
                    }
                }

                $platformMenu = WebshopMenu::firstOrNew([
                    'main_menu_id' => $platformMaster->main_menu_id,
                    'delivery_platform_id' => $slave->id,
                    'platform_id' => $platformId,
                    'status' => $platformMaster->status,
                    'outlet_id' => $slave->outlet_id,
                    'day' => $platformMaster->day,
                    'submenu_id' => $platformMaster->submenu_id,
                ]);
                $platformMenu->menu = serialize($result);
                $platformMenu->category_ids = serialize($categoryIds);
                $platformMenu->from = $platformMaster->from;
                $platformMenu->to = $platformMaster->to;
                $platformMenu->save();
                DB::table('delivery_platform')
                    ->where('id', $slave->id)
                    ->update(['selected_menu' => $platformMaster->main_menu_id]);
            }
        }
    }

    public function logMenuHistory($batch_id, $main_menu, $user_id, $master_outlet, $sub_outlet, $menu_json, $item_ids, $delivery_platform_id, $submenuId, $day, $categories)
    {
        try {
            $log = new MenuHistory;
            $log->batch_id = $batch_id;
            $log->main_menu = $main_menu;
            $log->delivery_platform_id = $delivery_platform_id;
            $log->user_id = $user_id;
            $log->master_outlet = $master_outlet;
            $log->submenu_id = $submenuId;
            $log->day = $day;
            $log->category_ids = serialize($categories);
            $log->sub_outlet = serialize($sub_outlet);
            $log->menu_json = serialize($menu_json);
            $log->item_ids = serialize($item_ids);
            $log->save();
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error($e->getMessage());
        }
    }

    public function filterCategoryItems($id, $dp = null, $active = true, $query = '', $category, $outlet = null)
    {
        try {
            $result = $this->getWebshopCategoryItemsByMenuIdAndShopId($id, $dp, $active, $query, $outlet);
            if ($result->getStatusCode()!=200) {
                return $result;
            }
            $result = (array)(json_decode($result->getContent())->data);

            $categoryItems = [];
            if (array_key_exists($category, $result)) {
                $categoryItems = $result[$category];
            }
            return $this->success('Category items.', $categoryItems);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error($e->getMessage());
        }
    }

    public function getScheduledMainMenus()
    {
        try {
            $bufferTime = DateTimeUtility::addRemoveDaysFromDate('Now', '- 300 minutes', 'Y-m-d H:i:s');
            $scheduledMenus = MainMenuSchedular::where('publishable_date', '>', $bufferTime)->orderBy('publishable_date', 'ASC')->with(['mainMenu'])->get();
            $scheduleList = [];
            foreach ($scheduledMenus as $key => $menu) {
                $menu->scheduled_date = DateTimeUtility::getDateTimeFormat($menu->publishable_date, 'Y-m-d');
                $menu->scheduled_time = DateTimeUtility::getDateTimeFormat($menu->publishable_date, 'H:i:s');
                $menu->platform_ids   = (is_null($menu->platform_ids)?[]:unserialize($menu->platform_ids));
                $scheduleList[] = $menu;
            }
            return $this->success('Scheduled menus.', $scheduleList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function createScheduledMainMenu($data)
    {
        try {
            $canPublish = true;
            $mainMenu = MainMenu::find($data['main_menu_id']);
            if (count($mainMenu->menus->where('main_menu_id', $mainMenu->id))>0) {
                foreach ($mainMenu->menus->where('main_menu_id', $mainMenu->id) as $key => $menu) {
                    if (count(unserialize($menu->service_availability))==0) {
                        $canPublish = false;
                    }
                }
            } else {
                $canPublish = false;
            }

            if (!$canPublish) {
                return $this->error('Menu time is not configured. Please set the time before schedule it.');
            }
            $scheduledMenu = new MainMenuSchedular;
            $scheduledMenu->main_menu_id        = $data['main_menu_id'];
            $scheduledMenu->publishable_date    = DateTimeUtility::getDateTimeFormat($data['publishable_date'], 'Y-m-d H:i:s');
            $scheduledMenu->status              = $data['status'];
            $scheduledMenu->platform_ids        = serialize($data['platform_ids']);
            $scheduledMenu->save();
            CommonHelper::userLog(null, ['description' => 'Created scheduled main menu', 'event' => 'create', 'subject_type' => 'main_menu', 'subject_id' => $data['main_menu_id']]);
            return $this->success('Created the scheduled menu.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function showScheduledMainMenu($id)
    {
        try {
            $scheduledMenu = MainMenuSchedular::find($id);
            if (is_null($scheduledMenu)) {
                return $this->notFound('Scheduled menu not found.');
            }
            $scheduledMenu = $scheduledMenu->fresh(['mainMenu']);
            $scheduledMenu->scheduled_date = DateTimeUtility::getDateTimeFormat($scheduledMenu->publishable_date, 'Y-m-d');
            $scheduledMenu->scheduled_time = DateTimeUtility::getDateTimeFormat($scheduledMenu->publishable_date, 'H:i:s');
            $scheduledMenu->platform_ids   = (is_null($scheduledMenu->platform_ids)?[]:unserialize($scheduledMenu->platform_ids));

            return $this->success('Retrieved the scheduled menu.', $scheduledMenu);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function updateScheduledMainMenu($data, $id)
    {
        try {
            $canPublish = true;
            $mainMenu = MainMenu::find($data['main_menu_id']);
            if (count($mainMenu->menus->where('main_menu_id', $mainMenu->id))>0) {
                foreach ($mainMenu->menus->where('main_menu_id', $mainMenu->id) as $key => $menu) {
                    if (count(unserialize($menu->service_availability))==0) {
                        $canPublish = false;
                    }
                }
            } else {
                $canPublish = false;
            }

            if (!$canPublish) {
                return $this->error('Menu time is not configured. Please set the time before schedule it.');
            }
            $scheduledMenu = MainMenuSchedular::find($id);
            $scheduledMenu->main_menu_id        = $data['main_menu_id'];
            $scheduledMenu->publishable_date    = DateTimeUtility::getDateTimeFormat($data['publishable_date'], 'Y-m-d H:i:s');
            $scheduledMenu->platform_ids        = serialize($data['platform_ids']);
            $scheduledMenu->status              = $data['status'];
            $scheduledMenu->save();
            CommonHelper::userLog(null, ['description' => 'Updated scheduled main menu', 'event' => 'update', 'subject_type' => 'main_menu', 'subject_id' => $data['main_menu_id']]);
            return $this->success('Updated the scheduled menu.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function destroyScheduledMainMenu($id)
    {
        try {
            $scheduledMenu = MainMenuSchedular::find($id);
            $mainMenuId = $scheduledMenu->main_menu_id;
            $scheduledMenu->delete();
            CommonHelper::userLog(null, ['description' => 'Deleted scheduled main menu', 'event' => 'delete', 'subject_type' => 'main_menu', 'subject_id' => $mainMenuId]);
            return $this->success('Deleted the scheduled menu.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getMainMenuByBrandAndShop($brandId, $shopId)
    {
        try {
            $mainMenu_arr = [];
            $mainMenus = $this->getMainMenu($brandId);
            $mainMenus = (json_decode($mainMenus->getContent()))->data->menus;
            foreach ($mainMenus as $key => $mainMenu) {
                // Get main platform IDs from the menu
                $platformIds = (array)$mainMenu->platform_ids;
                // Get child platforms where is_master = 0 and parent_platform is in platform_ids
                $childPlatforms = DB::table('delivery_platform')
                    ->where('is_master', 0)
                    ->whereIn('parent_platform', $platformIds)
                    ->where('webshop_brand_id', $brandId)
                    ->where('outlet_id', $shopId)
                    ->pluck('id')
                    ->toArray();
                $allPlatformIds = array_values(array_unique(array_merge($platformIds, $childPlatforms)));

                $dps = DB::table('delivery_platform')
                    ->whereIn('id', $allPlatformIds)
                    ->where('webshop_brand_id', $brandId)
                    ->where('outlet_id', $shopId)
                    ->get();
                if (count($dps) > 0) {
                    if ($mainMenu->status == 'ACTIVE') {
                        $mainMenu_arr[] = $mainMenu;
                    }
                }
            }
            return $this->success('Main menus by brand\'s outlet.', $mainMenu_arr);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getSnoozeItemList($id, $query = '')
    {
        try {
            $menuService = new MenuService;
            $shop = Shop::find($id);
            if (is_null($shop)) {
                return $this->notFound('Not found');
            }

            $selected_menu_ids = DB::table('delivery_platform')->where('outlet_id', $id)->whereNotNull('selected_menu')->pluck('selected_menu')->unique()->toArray();
            if (count($selected_menu_ids) > 0) {
                $mainMenuIds = $selected_menu_ids;
            } else {
                $mainMenuIds = ShopMainMenu::where('shop_id', $id)->pluck('main_menu_id')->toArray();
            }
            $mainMenus = MainMenu::whereIn('id', $mainMenuIds)->where('status', 'ACTIVE')->get();
            $selected_menu_ids = MainMenu::whereIn('id', $selected_menu_ids)->where('status', 'ACTIVE')->pluck('id')->toArray();

            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platforms = (json_decode($response->getBody()))->data;
            $platformList = [];
            $dpIds = [];
            foreach ($platforms as $key => $platform) {
                $platformList[$platform->id] = ['id' => $platform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $platform->name))), 'logo' => $platform->logo];
                if (!is_null($shop) && ($platform->outlet_id==$shop->id)) {
                    $dpIds[] = $platform->id;
                }
            }

            $mainMenuArray = [];
            foreach ($mainMenus as $key => $mainMenu) {
                $mainMenuArray[$mainMenu->id] = $mainMenu;
            }
            $mainMenuIds = $mainMenus->pluck('id')->toArray();

            $itemIds = [];
            $platformIds = [];
            foreach ($mainMenuArray as $key => $mainMenu) {
                $itemIds = array_merge($itemIds, (is_array($unserializedItemIds = unserialize($mainMenu->item_ids)) ? $unserializedItemIds : []));
                $platformIds = array_merge($platformIds, (is_array($unserializedPlatformIds = unserialize($mainMenu->platform_ids)) ? $unserializedPlatformIds : []));
            }

            $modifierGroups = ModifierGroup::whereIn('main_menu_id', $mainMenuIds)->get();
            $modifierItemList = [];
            $modifierNameList = [];
            $queryItemIds = [];
            foreach ($modifierGroups as $key => $modifierGroup) {
                foreach ($modifierGroup->mainItems as $key1 => $mainItem) {
                    if (!is_null($mainItem->alternativeItem) && !is_null($mainItem->alternativeItem->entity_id)) {
                        foreach ($modifierGroup->items as $key2 => $modItem) {
                            if (!is_null($modItem->item) && !is_null($modItem->item->entity_id)) {
                                if (!isset($modifierNameList[$mainItem->alternativeItem->entity_id][$modItem->item->entity_id]) || !in_array($modifierGroup->title, $modifierNameList[$mainItem->alternativeItem->entity_id][$modItem->item->entity_id])) {
                                    if (isset($modifierItemList[$mainItem->alternativeItem->entity_id][$modItem->item->entity_id])) {
                                        $modifierItemList[$mainItem->alternativeItem->entity_id][$modItem->item->entity_id]['modifier_names'][] = $modifierGroup->title;
                                    } else {
                                        $snoozedItems = EntityDeliveryPlatform::where('entity_id', $modItem->item->entity_id)->where('available', 0)->get();
                                        $modifierItemList[$mainItem->alternativeItem->entity_id][$modItem->item->entity_id] = ['name' => $modItem->item->item_name, 'id' => $modItem->item->entity_id, 'snooze_available' => (count($snoozedItems)>0?0:1), 'available_from' => (count($snoozedItems)==0?null:DateTimeUtility::getDateTimeFormat($snoozedItems->first()->available_from, 'Y-m-d H:i:s')), 'modifier_names' => [$modifierGroup->title]];
                                    }
                                    $modifierNameList[$mainItem->alternativeItem->entity_id][$modItem->item->entity_id][] = $modifierGroup->title;
                                }
                            }
                        }
                    }
                }
            }
            $finalModifierList = [];
            foreach ($modifierItemList as $key => $mainItem) {
                foreach ($mainItem as $keyInner => $subItem) {
                    $finalModifierList[$key][$keyInner] = $subItem;
                    if ($query != '' && str_contains(strtolower($subItem['name']), strtolower($query))) {
                        $queryItemIds[] = $key;
                    }
                    if (isset($modifierItemList[$keyInner])) {
                        foreach ($modifierItemList[$keyInner] as $keyIn => $item) {
                            if ($keyIn != $key) {
                                if (isset($finalModifierList[$key][$keyIn])) {
                                    $mergedModifierNames = array_merge($finalModifierList[$key][$keyIn]['modifier_names'], $item['modifier_names']);
                                    $finalModifierList[$key][$keyIn]['modifier_names'] = array_unique($mergedModifierNames);
                                } else {
                                    $finalModifierList[$key][$keyIn] = $item;
                                }
                                if ($query != '' && str_contains(strtolower($item['name']), strtolower($query))) {
                                    $queryItemIds[] = $key;
                                }
                            }
                        }
                    }
                }
            }

            $itemIds = array_unique($itemIds);
            $queryItemIds = array_unique($queryItemIds);
            $platformIds = array_unique($platformIds);
            if ($query == '') {
                $items = Item::select('id', 'title', 'image_url')->whereIn('id', $itemIds)->where('status', 1)->has('categories', '>', 0)->has('entityDeliveryPlatform', '>', 0)->whereHas('entityDeliveryPlatform', function ($q) use ($platformIds) {
                        $q->whereIn('delivery_platform_id', $platformIds);
                    })->orderBy('title', 'ASC')->get();
            } else {
                $items = Item::select('id', 'title', 'image_url')->whereIn('id', $itemIds)->where(function ($searchQuery) use ($query, $queryItemIds) {
                    $searchQuery->where('title', 'LIKE', '%' . $query . '%');
                    $searchQuery->orWhereIn('id', $queryItemIds);
                })->where('status', 1)->has('categories', '>', 0)->has('entityDeliveryPlatform', '>', 0)->whereHas('entityDeliveryPlatform', function ($q) use ($platformIds) {
                    $q->whereIn('delivery_platform_id', $platformIds);
                })->orderBy('title', 'ASC')->get();
            }

            $snoozed = 0;
            $unsnoozed = 0;

            foreach ($items as $key => $item) {
                $item->snooze_available = 1;
                $priceList = $item->prices->whereIn('main_menu_id', $mainMenuIds);
                $commonDPIds = array_intersect($dpIds, $priceList->pluck('delivery_platform_id')->toArray());
                $platforms = [];
                $addedPlatformIds = [];
                $modifiers = [];
                $modifierItemNames = [];

                foreach ($item->entityDeliveryPlatform->whereIn('delivery_platform_id', $commonDPIds) as $enkey => $entityItem) {
                    if (!in_array($entityItem->delivery_platform_id, $addedPlatformIds) && isset($platformList[$entityItem->delivery_platform_id])) {
                        $platforms[] = $platformList[$entityItem->delivery_platform_id];
                        $addedPlatformIds[] = $entityItem->delivery_platform_id;
                    }
                    if (!$entityItem->available) {
                        $item->snooze_available = 0;
                    }
                }
                if (isset($finalModifierList[$item->id])) {
                    $modifiers = array_values($finalModifierList[$item->id]);
                }
                if ($item->snooze_available) {
                    $unsnoozed++;
                } else {
                    $snoozed++;
                }
                $item->modifiers = $modifiers;
                $item->platforms = $platforms;
                unset($item->prices);
                unset($item->entityDeliveryPlatform);
            }
            return $this->success('Snooze list', ['snoozed_count' => $snoozed, 'unsnoozed_count' => $unsnoozed, 'items' => $items]);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getWebshopMenuTimes($id, $dp, $shopId)
    {
        try {
            $mainMenu = MainMenu::find($id);
            if (is_null($mainMenu)) {
                return $this->notFound('Main menu not found.');
            }
            if (count($mainMenu->menus->where('main_menu_id', $mainMenu->id)) == 0) {
                return $this->error('Menu does not contain any sub menus.');
            }
            $menuTimes = null;
            $shop = Shop::find($shopId);
            if (is_null($shop)) {
                return $this->notFound('Shop not found.');
            }
            $day =  strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Now', 'l', $shop->timezone));
            $time =  DateTimeUtility::getDateTimeFormatWithTimeZone('Now', 'H:i:s', $shop->timezone);
            $webshopMenu = [];
            if (!is_null($mainMenu)) {
                $submenuIds = $mainMenu->menus->pluck('id')->toArray();

                // Build base query conditions
                $baseConditions = [
                    'main_menu_id' =>  $id,
                    'delivery_platform_id' => $dp,
                    'status' => 1,
                    'outlet_id' => $shopId
                ];

                $webshopMenu = $this->findWebshopMenu($baseConditions, $submenuIds, $day, $time, $shop->timezone);

                // $webshopMenu = WebshopMenu::where('main_menu_id', $id)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp)->where('status', 1)->where('outlet_id', $shopId)->where('day', $day)->whereTime('from', '<=', $time)->whereTime('to', '>=', $time)->orderBy('id', 'DESC')->get();

                // if (count($webshopMenu)==0) {
                //     $previousDay = strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Yesterday', 'l', $shop->timezone));
                //     $webshopMenu = WebshopMenu::where('main_menu_id', $id)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp)->where('status', 1)->where('outlet_id', $shopId)->where('day', $previousDay)/*->whereTime('from', '>=', $time)*/->whereTime('to', '>=', $time)->orderBy('id', 'DESC')->get();
                //     if (count($webshopMenu)==0 || ($webshopMenu->first()->from < $webshopMenu->first()->to)) {
                //         $webshopMenu = WebshopMenu::where('main_menu_id', $id)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp)->where('status', 1)->where('outlet_id', $shopId)/*->where('day', $day)->whereTime('to', '>=', $time)*/->orderBy('id', 'DESC')->get();
                //     }
                // }
            }
            if (count($webshopMenu)>0) {
                $webshopMenu = $webshopMenu->first();
                $menu = $webshopMenu->submenu_id;
            }
            if (count($mainMenu->menus->where('main_menu_id', $mainMenu->id)) == 1) {
                if (!is_null($shop->service_availability)) {
                    $menuTimes = unserialize($shop->service_availability);
                }
            }
            $dayNames = ['monday' => 'Mon', 'tuesday' => 'Tue', 'wednesday' => 'Wed', 'thursday' => 'Thu', 'friday' => 'Fri', 'saturday' => 'Sat', 'sunday' => 'Sun'];
            if (is_null($menuTimes)) {
                $menuObj = Menu::find($menu);
                if (is_null($menuObj)) {
                    return $this->notFound('Sub menu not found.');
                }
                $menuTimes = unserialize($menuObj->service_availability);
            }
            $menuTimesList = [];
            foreach ($menuTimes as $key => $menuTime) {
                if ($menuTime['availability']) {
                    $dayOfWeek = $dayNames[$menuTime['day_of_week']] ?? $menuTime['day_of_week'];
                    $menuTimesList[$dayOfWeek]['week_day'] = $dayOfWeek;
                    foreach ($menuTime['time_periods'] as $timeAv => $timeAvailability) {
                        $menuTimesList[$dayOfWeek]['times'][] = ['open_time' => $timeAvailability['start_time'], 'close_time' => $timeAvailability['end_time']];
                    }
                }
            }
            $dayOrder = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
            uksort($menuTimesList, function ($key1, $key2) use ($dayOrder) {
                return ((array_search($key1, $dayOrder) > array_search($key2, $dayOrder)) ? 1 : -1);
            });
            $menuTimesList = array_values($menuTimesList);
            return $this->success('Menu Times', $menuTimesList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function updateSnoozeItemListJson($id)
    {
        try {
            $result = $this->getSnoozeItemList($id);
            if ($result->getStatusCode() != 200) {
                return $result;
            }
            $result = (array)(json_decode($result->getContent())->data);
            DB::transaction(function () use (&$id, &$result) {
                $shopSnoozeItemList = ShopSnoozeItem::firstOrNew([
                    'outlet_id' => $id
                ]);
                $shopSnoozeItemList->item_list = serialize($result);
                $shopSnoozeItemList->save();
            });
            \Log::info('Shop ID : ' . $id . ' snooze item list updated' . '. - '.CommonHelper::getXTenantCode($_SERVER));
            return $this->success('Snooze item list updated', $result);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getOptimizedSnoozeItemList($id, $data)
    {
        try {
            $itemList = [];
            $shopSnoozeItemList = ShopSnoozeItem::where('outlet_id', $id)->first();
            if (!is_null($shopSnoozeItemList)) {
                $itemList = (!is_null($shopSnoozeItemList->item_list) ? unserialize($shopSnoozeItemList->item_list) : []);
            }
            if (count($itemList) == 0) {
                $query = (isset($data['q']) ? $data['q'] : '');
                $itemList = $this->getSnoozeItemList($id, $query);
                if (!is_array($itemList) && $itemList->getStatusCode() == 200) {
                    $itemList = json_decode($itemList->getContent())->data;
                }
            }
            return $this->success('Snooze list', $itemList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }
}
