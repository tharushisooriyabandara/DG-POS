<?php

namespace App\Http\Services;

use App\Http\Helpers\CommonHelper;
use App\Http\Models\Category;
use App\Http\Models\CategoryMenu;
use App\Http\Models\CustomerFavouriteItem;
use App\Http\Models\EntityDeliveryPlatform;
use App\Http\Models\Images;
use App\Http\Models\Item;
use App\Http\Models\ItemCategory;
use App\Http\Models\ItemPrice;
use App\Http\Models\ItemTax;
use App\Http\Models\MainMenu;
use App\Http\Models\MainMenuMenu;
use App\Http\Models\Menu;
use App\Http\Models\ModifierGroup;
use App\Http\Models\ModifierGroupItem;
use App\Http\Models\ModifierGroupModifierItem;
use App\Http\Models\PosCategory;
use App\Http\Models\PosItem;
use App\Http\Models\ShopMainMenu;
use App\Http\Models\VariantStore;
use App\Http\Models\WebshopMenu;
use App\Http\Services\CategoryService;
use App\Http\Services\ImageService;
use App\Http\Services\MenuService;
use App\Http\Services\PosService;
use App\Jobs\UpdatePosWebshopMenu;
use App\Jobs\UpdateSnoozeItemList;
use App\microservice_delivergate_api\Models\Shop;
use App\microservice_delivergate_api\Services\BaseService as BaseService;
use App\microservice_delivergate_api\Services\RequestHandleService;
use DateTimeUtility;
use Exception;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\Config;
use Illuminate\Support\Facades\DB;

class ItemService extends BaseService
{
    private $client;
    private $menu_service;
    private $image_service;
    private $request_handle_service;

    public function __construct()
    {
        $this->client = new \GuzzleHttp\Client ();
        $this->menu_service = new MenuService;
        $this->image_service = new ImageService;
        $this->request_handle_service = new RequestHandleService;
    }

    public function getItems($main_menu, $itemId = null)
    {
        try {
            if (is_null($itemId)) {
                $itemList = Item::all()->fresh('entityDeliveryPlatform', 'prices', 'categories');
            } else {
                $itemList = Item::where('id', $itemId)->get()->fresh('entityDeliveryPlatform', 'prices', 'categories');
            }

            $mainMenu = MainMenu::find($main_menu);
            $allowedModifierIds = (is_null($mainMenu)?[]:$mainMenu->modifiers->pluck('id')->toArray());
            $masterOutletDeliveryIds = [];
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
                if (!is_null($mainMenu) && $deliveryPlatform->outlet_id == $mainMenu->master_outlet) {
                    $masterOutletDeliveryIds[] = $deliveryPlatform->id;
                }

                $deliveryPlatformList[$deliveryPlatform->id] = ['id' => $deliveryPlatform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $deliveryPlatform->name))), 'logo' => $platformList[$deliveryPlatform->platform_id]];
            }

            foreach ($itemList as $key => $item) {
                $item->is_valid_image = (is_null($item->image_url) ? true : (in_array(strtoupper(pathinfo($item->image_url, PATHINFO_EXTENSION)), ['JPEG', 'JPG', 'PNG']) ? true : false));
                $item->masterEntityDeliveryPlatforms = $item->entityDeliveryPlatform->whereIn('delivery_platform_id', $masterOutletDeliveryIds);
                $platform_urls = [];
                $item->categoryList = $item->categoriesByMainMenu($main_menu);
                $itemPrice = $item->prices->where('main_menu_id', $main_menu)->where('delivery_platform_id', null);
                $availablePlatformIds = $item->prices->where('main_menu_id', $main_menu)->whereNotNull('delivery_platform_id')->pluck('delivery_platform_id')->toArray();
                $item->availablePlatformIds = $availablePlatformIds;
                if (count($itemPrice) > 0) {
                    $item->price = $itemPrice->first()->price;
                }

                $modifiers = null;
                $allergies = [];
                $tmpPlatforms = [];
                $is_modifier = false;
                $entityDeliveryPlatforms = [];
                $masterEntityDeliveryPlatforms = [];
                $modifierList = [];
                foreach ($item->masterEntityDeliveryPlatforms as $key => $masterEntity) {
                    foreach ($masterEntity->modifiers->whereIn('modifier_group_id', $allowedModifierIds) as $mkey => $mod) {
                        $modifierList[$mod->modifier_group_id] = $mod;
                    }
                    $masterEntity->allergies = (unserialize($masterEntity->allergies) == false ? [] : unserialize($masterEntity->allergies));
                    if (count($allergies) < count($masterEntity->allergies)) {
                        $allergies = $masterEntity->allergies;
                    }
                    if (!$is_modifier) {
                        $is_modifier = CommonHelper::isModifier($masterEntity->external_item_id);
                    }
                    $tmpPrice = $item->prices->where('main_menu_id', $main_menu)->where('delivery_platform_id', $masterEntity->delivery_platform_id);
                    if (count($tmpPrice) > 0) {
                        $masterEntity->price = $tmpPrice->first()->price;
                    }
                    if (in_array($masterEntity->delivery_platform_id, $availablePlatformIds)) {
                        $masterEntityDeliveryPlatforms[] = $masterEntity;
                        if (!in_array($masterEntity->delivery_platform_id, $tmpPlatforms) && !is_null($main_menu)) {
                            $platform_urls[] = $deliveryPlatformList[$masterEntity->delivery_platform_id];
                            $tmpPlatforms[] = $masterEntity->delivery_platform_id;
                        }
                    }
                }
                $modifiers = array_values($modifierList);
                foreach ($item->entityDeliveryPlatform as $key1 => $entity) {
                    $tmpPrice = $item->prices->where('main_menu_id', $main_menu)->where('delivery_platform_id', $entity->delivery_platform_id);
                    if (count($tmpPrice) > 0) {
                        $entity->price = $tmpPrice->first()->price;
                    }
                    if (in_array($entity->delivery_platform_id, $availablePlatformIds)) {
                        $entityDeliveryPlatforms[] = $entity;
                    }
                }
                if (!is_null($main_menu)) {
                    $item->entityDeliveryPlatform = $entityDeliveryPlatforms;
                    $item->masterEntityDeliveryPlatforms = $masterEntityDeliveryPlatforms;
                }
                $item->modifiers = $modifiers;
                $item->allergies = $allergies;
                $item->platform_urls = $platform_urls;
                $item->is_modifier = $is_modifier;
            }
            return $this->success('Items', $itemList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function store($data)
    {
        try {
            $item = new Item;
            $imagePaths = [];
            if (isset($data['id'])) {
                $item->id = $data['id'];
            }
            if (isset($data['image']) && $data['image'] != '') {
                $imagePaths = $this->image_service->resizeAndUploadImageToCloud($data['image'], 'products');
                $item->image_url = $imagePaths['medium'];
            } elseif (isset($data['image_url']) && $data['image_url'] != '') {
                //$item->image_url = $data['image_url'];
                $imagePaths = $this->image_service->resizeAndUploadImageToCloudByUrl($data['image_url'], 'products');
                $item->image_url = $imagePaths['medium'];
            }

            $item->remote_id = (isset($data['remote_id']) ? ($data['remote_id']) : 0);
            $item->title = $data['title'];
            $item->description = (isset($data['description']) ? ($data['description']) : null);
            $item->tax = (isset($data['tax']) ? ($data['tax']) : 0);
            $item->tax_profile_id = (isset($data['tax_profile_id']) ? ($data['tax_profile_id'] != 'null' ? $data['tax_profile_id'] : null) : null);
            $item->price = (isset($data['price']) ? ($data['price']) : 0);
            $item->sku = ((isset($data['sku']) && $data['sku'] != 'null') ? ($data['sku']) : null);
            $item->status = $data['status'];
            $item->contains_alcohol = (isset($data['contains_alcohol']) ? ($data['contains_alcohol']) : 0);
            $item->size = (isset($data['size']) ? (strtolower($data['size'])) : 'medium');
            $item->weight = (isset($data['weight']) ? $data['weight'] : null);
            DB::transaction(function () use ($item, $data, $imagePaths) {
                $item->save();

                $group = CommonHelper::generateRandomCode(8);
                foreach ($imagePaths as $imkey => $path) {
                    $itemImage = Images::firstOrNew([
                        'type' => 'ITEM',
                        'type_id' => $item->id,
                        'size' => $imkey,
                    ]);
                    $itemImage->group = $group;
                    $itemImage->path = $path;
                    $itemImage->save();
                }
                $input = ['item_name' => $data['title'], 'price' => $item->price, 'sku' => $item->sku, 'description' => $item->description, 'item_id' => $item->id];
                if (isset($data['main_menu'])) {
                    $itmPrice = ItemPrice::firstOrNew([
                        'main_menu_id' => $data['main_menu'],
                        'entity_item_id' => $item->id,
                    ]);
                    $itmPrice->price = $data['price'];
                    $itmPrice->save();
                }

                if (isset($data['category_id']) && $data['category_id'] != '') {
                    if (isset($data['pos_id'])) {
                        $catId = PosCategory::where('remote_id', $data['category_id'])->where('pos_id', $data['pos_id'])->get()->first();
                        $catId = $catId->category_id;
                    } else {
                        // NOTE This can be issue if catogory id is refer to remote id
                        // $catId = PosCategory::where('remote_id', $data['category_id'])->get()->first();
                        $catId = Category::find($data['category_id']);
                        $catId = $catId->id;
                    }
                    $input['category_id'] = $catId;
                    // Change category_id to id
                    $itmCat = ItemCategory::firstOrNew([
                        'item_id' => $item->id,
                        'main_menu_id' => (isset($data['main_menu']) ? $data['main_menu'] : null),
                    ]);
                    $itmCat->category_id = $catId;
                    $itmCat->save();
                }

                if (isset($data['delivery_platforms'])) {
                    foreach ($data['delivery_platforms'] as $key => $platform) {
                        $properties = ['allergies' => (isset($data['allergies']) ? $data['allergies'] : []), 'modifier_ids' => ((isset($data['modifier_ids']) && $data['modifier_ids'] != '') ? $data['modifier_ids'] : [])];
                        $properties['delivery_platform'] = $platform;
                        $properties['item_id'] = $item->id;
                        $properties['remote_id'] = $item->id;
                        $properties['plu'] = $item->id;
                        $properties['price'] = $item->price * 100;
                        $properties['title'] = $item->title;
                        $this->createDeliveryPlatformItem($properties);
                    }
                }

                if (isset($data['modifier_ids'])) {
                    $this->itemModifierStore($data['modifier_ids'], $item->id, null);
                }
                if (!isset($data['remote_id']) && isset($data['category_id'])) {
                    try {
                        $posService = new PosService;
                        $response = $posService->createRemoteItem($input);
                    } catch (Exception $e) {
                        $this->loggerError($e, $this, __FUNCTION__, __LINE__);
                    }
                }
            });
            CommonHelper::userLog(null, ['description' => 'Created item titled "' . $item->title . '"', 'event' => 'create', 'subject_type' => 'item', 'subject_id' => $item->id]);
            return $this->success('Successfully created the item.', $item);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function show($id, $main_menu)
    {
        try {
            $item = Item::find($id);
            if (is_null($item)) {
                return $this->notFound('Item not found');
            }
            if (!is_null($main_menu)) {
                $itemPrice = $item->prices->where('main_menu_id', $main_menu);
                if (count($itemPrice) > 0) {
                    $item->price = $itemPrice->first()->price;
                }
            }
            return $this->success('Item', $item);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function getRemoteItem($platform, $id)
    {
        try {
            $entityItem = EntityDeliveryPlatform::where('delivery_platform_id', $platform)->where('plu', $id)->get();
            $entityItem = $entityItem->first();
            if (is_null($entityItem)) {
                if (CommonHelper::getOrderSyncStatus($_SERVER)) {
                    return $this->notFound('Item not found. Platform ID: ' . $platform . ', Plu: ' . $id . ', Tenant code: ' . CommonHelper::getXTenantCode($_SERVER));
                } else {
                    return $this->success('Item not found. Platform ID: ' . $platform . ', Plu: ' . $id . ', Tenant code: ' . CommonHelper::getXTenantCode($_SERVER), null);
                }
            }
            $item = Item::find($entityItem->entity_id);
            if (is_null($item)) {
                if (CommonHelper::getOrderSyncStatus($_SERVER)) {
                    return $this->notFound('Item not found. Platform ID: ' . $platform . ', Plu: ' . $id . ', Tenant code: ' . CommonHelper::getXTenantCode($_SERVER));
                } else {
                    return $this->success('Item not found. Platform ID: ' . $platform . ', Plu: ' . $id . ', Tenant code: ' . CommonHelper::getXTenantCode($_SERVER), null);
                }
            }
            return $this->success('Item', $item->fresh('categories'));
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function update($data, $id)
    {
        try {
            if (isset($data['loyverse_item'])) {
                $item = Item::where('variant_id', $id)->first();
            } elseif (isset($data['loyverse'])) {
                if (isset($data['remote_id'])) {
                    $item = Item::where('title', $id)->where('remote_id', $data['remote_id'])->first();
                    if (is_null($item)) {
                        $item = Item::where('title', $id)->first();
                    }
                    $item->remote_id = $data['remote_id'];
                } else {
                    $item = Item::where('title', $id)->first();
                }
            } else {
                $item = Item::find($id);
            }

            if (is_null($item)) {
                return $this->notFound('Item not found');
            }

            $imagePaths = [];
            $item->title = $data['title'];
            $item->description = (isset($data['description']) ? ($data['description']) : null);
            if (isset($data['image']) && $data['image'] != '') {
                $imagePaths = $this->image_service->resizeAndUploadImageToCloud($data['image'], 'products');
                $item->image_url = $imagePaths['medium'];
            } elseif (!isset($data['image_url']) && !isset($data['image']) && empty($data['image'])) {
                $item->image_url = null;
                $this->deleteItemImage($item->id);
            } elseif (is_null($item->image_url) && isset($data['image_url']) && $data['image_url'] != '' && $data['image_url'] != 'null') {
                //$item->image_url = $data['image_url'];
                $imagePaths = $this->image_service->resizeAndUploadImageToCloudByUrl($data['image_url'], 'products');
                $item->image_url = $imagePaths['medium'];
            }
            if (isset($data['loyverse_item'])) {
                $item->cost = $data['cost'];
                $item->color = $data['color'];
                $item->handle = $data['handle'];
                $item->variant_id = $data['variant_id'];
                $item->sku = $data['sku'];
                $item->cost = $data['cost'];
                $item->purchase_cost = $data['purchase_cost'];
                $item->store_id = $data['store_id'];
            }

            $item->tax = (isset($data['tax']) ? ($data['tax']) : $item->tax);
            $item->tax_profile_id = (isset($data['tax_profile_id']) ? ($data['tax_profile_id'] != 'null' ? $data['tax_profile_id'] : null) : $item->tax_profile_id);
            $item->source = (isset($data['source']) ? ($data['source']) : $item->source);
            $item->price = (isset($data['price']) ? ($data['price']) : $item->price);
            $item->sku = (isset($data['sku']) ? ($data['sku'] == 'null' ? null : $data['sku']) : $item->sku);
            $item->status = $data['status'];
            $item->contains_alcohol = (isset($data['contains_alcohol']) ? $data['contains_alcohol'] : $item->contains_alcohol);
            $item->size = (isset($data['size']) ? (strtolower($data['size'])) : $item->size);
            $item->weight = (isset($data['weight']) ? $data['weight'] : $item->weight);
            if (!isset($data['main_menu'])) {
                $item->price = (isset($data['price']) ? ($data['price']) : $item->price);
            }
            DB::transaction(function () use (&$item, $data, $imagePaths) {
                $item->save();

                $group = CommonHelper::generateRandomCode(8);
                foreach ($imagePaths as $imkey => $path) {
                    $itemImage = Images::firstOrNew([
                        'type' => 'ITEM',
                        'type_id' => $item->id,
                        'size' => $imkey,
                    ]);
                    $itemImage->group = $group;
                    $itemImage->path = $path;
                    $itemImage->save();
                }
                EntityDeliveryPlatform::where('entity_id', $item->id)->update(['item_name' => $item->title]);
                $input = ['item_name' => $item->title, 'price' => $item->price, 'sku' => $item->sku, 'description' => $item->description, 'item_id' => $item->id, 'type' => 'UPDATE'];
                if (isset($data['main_menu']) && !isset($data['delivery_platforms'])) {
                    $itmPrice = ItemPrice::firstOrNew([
                        'main_menu_id' => $data['main_menu'],
                        'delivery_platform_id' => null,
                        'entity_item_id' => $item->id,
                    ]);
                    $itmPrice->price = (isset($data['price']) ? ($data['price']) : $item->price);
                    $itmPrice->save();
                }
                if (isset($data['main_menu']) && $data['main_menu'] != '' && isset($data['delivery_platforms'])) {
                    foreach ($data['delivery_platforms'] as $key => $platform) {
                        if (isset($data['price_' . $platform])) {
                            $itmPrice = ItemPrice::firstOrNew([
                                'main_menu_id' => $data['main_menu'],
                                'delivery_platform_id' => $platform,
                                'entity_item_id' => $item->id,
                            ]);
                            $itmPrice->price = $data['price_' . $platform];
                            $itmPrice->save();
                        }
                    }
                    ItemPrice::where('main_menu_id', $data['main_menu'])->where('entity_item_id', $item->id)->whereNotIn('delivery_platform_id', $data['delivery_platforms'])->delete();
                } elseif ((!isset($data['main_menu']) || (isset($data['main_menu']) && $data['main_menu'] == '')) && isset($data['delivery_platforms'])) {
                    ItemPrice::whereNull('main_menu_id')->where('entity_item_id', $item->id)->whereNotIn('delivery_platform_id', $data['delivery_platforms'])->delete();
                    //ItemPrice::where('entity_item_id', $item->id)->whereNotIn('delivery_platform_id', $data['delivery_platforms'])->delete();
                } elseif (isset($data['main_menu']) && $data['main_menu'] != '' && empty($data['delivery_platforms'])) {
                    ItemPrice::where('main_menu_id', $data['main_menu'])->where('entity_item_id', $item->id)->whereNotIn('delivery_platform_id', [])->delete();
                }

                // if (isset($data['category_id']) && $data['category_id']!='') {
                //     ItemCategory::where('item_id', $item->id)->where('main_menu_id', (isset($data['main_menu'])?$data['main_menu']:null))->delete();
                //     $itmCat = ItemCategory::firstOrNew([
                //         'category_id' => $data['category_id'],
                //         'item_id' => $item->id,
                //         'main_menu_id' => (isset($data['main_menu']) ? $data['main_menu'] : null),
                //     ]);
                //     $itmCat->save();
                // }

                if (isset($data['category_id']) && $data['category_id'] != '') {
                    ItemCategory::where('item_id', $item->id)->where('main_menu_id', (isset($data['main_menu']) ? $data['main_menu'] : null))->delete();
                    if (isset($data['loyverse_item'])) {
                        $categoryNew = Category::where('remote_id', $data['category_id'])->first();
                        if (!is_null($categoryNew)) {
                            $itmCat = ItemCategory::firstOrNew([
                                'item_id' => $item->id,
                                'main_menu_id' => (isset($data['main_menu']) ? $data['main_menu'] : null),
                            ]);
                            $itmCat->category_id = $categoryNew->id;
                        }
                    } else {
                        $itmCat = ItemCategory::firstOrNew([
                            'item_id' => $item->id,
                            'main_menu_id' => (isset($data['main_menu']) ? $data['main_menu'] : null),
                        ]);
                        $itmCat->category_id = $data['category_id'];
                    }
                    $itmCat->save();
                    $input['category_id'] = $itmCat->category_id;
                } elseif ((isset($data['category_id']) && ($data['category_id'] == '') || is_null($data['category_id']))) {
                    ItemCategory::where('item_id', $item->id)->where('main_menu_id', (isset($data['main_menu']) ? $data['main_menu'] : null))->delete();
                }

                if (isset($data['loyverse_item'])) {
                    if (isset($data['modifier_ids'])) {
                        ModifierGroupItem::whereNotIn('modifier_group_id', $data['modifier_ids'])->where('item_id', (string) $item->id)->where('platform', 'LOYVERSE')->delete();
                        $this->itemModifierStore($data['modifier_ids'], $item->id, 'LOYVERSE');
                    } else {
                        ModifierGroupItem::where('item_id', (string) $item->id)->whereNull('platform')->delete();
                    }
                } else {
                    $connectedModifierItemIds = $item->entityDeliveryPlatform->pluck('external_item_id')->toArray();
                    if (isset($data['main_menu']) && $data['main_menu'] != '') {
                        $allowedModifierIds = ModifierGroup::where('main_menu_id', $data['main_menu'])->pluck('id')->toArray();
                        if (isset($data['modifier_ids'])) {
                            ModifierGroupItem::whereNotIn('modifier_group_id', $data['modifier_ids'])->whereIn('item_id', $connectedModifierItemIds)->whereNull('platform')->whereIn('modifier_group_id', $allowedModifierIds)->delete();
                            $this->itemModifierStore($data['modifier_ids'], $item->id, null);
                        } else {
                            ModifierGroupItem::whereIn('item_id', $connectedModifierItemIds)->where('platform', null)->whereIn('modifier_group_id', $allowedModifierIds)->delete();
                        }
                    } else {
                        if (isset($data['modifier_ids'])) {
                            ModifierGroupItem::whereNotIn('modifier_group_id', $data['modifier_ids'])->whereIn('item_id', $connectedModifierItemIds)->whereNull('platform')->delete();
                            $this->itemModifierStore($data['modifier_ids'], $item->id, null);
                        } else {
                            // ModifierGroupItem::whereIn('item_id', $connectedModifierItemIds)->where('platform', null)->delete();
                        }
                    }
                }

                if (isset($data['delivery_platforms'])) {
                    foreach ($data['delivery_platforms'] as $key => $platform) {
                        $properties = ['allergies' => (isset($data['allergies']) ? $data['allergies'] : []), 'modifier_ids' => ((isset($data['modifier_ids']) && $data['modifier_ids'] != '') ? $data['modifier_ids'] : [])];
                        $entity = EntityDeliveryPlatform::where('entity_id', $item->id)->where('delivery_platform_id', $platform)->get();
                        if (count($entity) > 0) {
                            $properties['title'] = $item->title;
                            //$properties['price'] = (isset($data['price_'.$platform])?$data['price_'.$platform]:$item->price)*100;
                            $this->updateDeliveryPlatformItem($properties, $entity->first()->id);
                        } else {
                            $properties['delivery_platform'] = $platform;
                            $properties['item_id'] = $item->id;
                            $properties['remote_id'] = $item->id;
                            $properties['plu'] = $item->id;
                            $properties['price'] = (isset($data['price_' . $platform]) ? $data['price_' . $platform] : $item->price) * 100;
                            $properties['title'] = $item->title;
                            $this->createDeliveryPlatformItem($properties);
                        }
                    }
                }

                if (isset($data['entity_item_ids'])) {
                    foreach ($data['entity_item_ids'] as $key => $itemId) {
                        $entityDeliveryPlatformItem = EntityDeliveryPlatform::find($itemId);
                        if (!is_null($entityDeliveryPlatformItem) && isset($data['item_name_' . $itemId])) {
                            $entityDeliveryPlatformItem->item_name = $data['item_name_' . $itemId];
                            $entityDeliveryPlatformItem->save();
                        }
                    }
                }

                if (!isset($data['loyverse_item']) && !isset($data['loyverse']) && isset($data['category_id'])) {
                    try {
                        $posService = new PosService;
                        $response = $posService->createRemoteItem($input);
                    } catch (Exception $e) {
                        $this->loggerError($e, $this, __FUNCTION__, __LINE__);
                    }
                }
            });
            $shopIds = [];
            if (isset($data['main_menu']) && $data['main_menu'] != '') {
                $shopIds = ShopMainMenu::where('main_menu_id', $data['main_menu'])->pluck('shop_id')->toArray();
            } else {
                $mainMenuIds = $item->prices->pluck('main_menu_id')->toArray();
                $mainMenuIds = array_unique($mainMenuIds);
                $shopIds = ShopMainMenu::whereIn('main_menu_id', $mainMenuIds)->distinct()->pluck('shop_id')->toArray();
            }
            // UpdateSnoozeItemList::dispatch(['shopIds' => $shopIds, 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
            CommonHelper::userLog(null, ['description' => 'Updated item', 'event' => 'update', 'subject_type' => 'item', 'subject_id' => $item->id]);
            return $this->success('Successfully updated the item.', $item);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function itemCategories($id)
    {
        try {
            $item = Item::find($id);
            if (is_null($item)) {
                return $this->notFound('Item not found');
            }
            return $this->success('Item categories', $item->categories);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function destroy($id, $main_menu)
    {
        try {
            $item = Item::find($id);
            if (is_null($item)) {
                return $this->notFound('Item not found');
            }
            $shopIds = [];
            DB::transaction(function () use ($item, $id, $main_menu, &$shopIds) {
                if (is_null($main_menu)) {
                    $mainMenuIds = $item->prices->pluck('main_menu_id')->toArray();
                    ItemCategory::where('item_id', $id)->delete();
                    EntityDeliveryPlatform::where('entity_id', $id)->delete();
                    ItemPrice::where('entity_item_id', $id)->delete();
                    ModifierGroupItem::where('item_id', (string) $id)->delete();
                    ModifierGroupModifierItem::where('item_id', (string) $id)->delete();
                    $item->delete();
                    $mainMenuIds = array_unique($mainMenuIds);
                    $shopIds = ShopMainMenu::whereIn('main_menu_id', $mainMenuIds)->distinct()->pluck('shop_id')->toArray();
                    CommonHelper::userLog(null, ['description' => 'Deleted item titled "' . $item->title . '"', 'event' => 'delete', 'subject_type' => 'item', 'subject_id' => $item->id]);
                } else {
                    ItemCategory::where('item_id', $id)->where('main_menu_id', $main_menu)->delete();
                    ItemPrice::where('entity_item_id', $id)->where('main_menu_id', $main_menu)->delete();
                    $shopIds = ShopMainMenu::where('main_menu_id', $main_menu)->pluck('shop_id')->toArray();
                }
            });
            // UpdateSnoozeItemList::dispatch(['shopIds' => $shopIds, 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
            return $this->success('Successfully deleted the item.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function deleteBulkItems($data)
    {
        try {
            DB::transaction(function () use ($data) {
                if (!isset($data['main_menu'])) {
                    ItemCategory::whereIn('item_id', $data['item_ids'])->delete();
                    ModifierGroupItem::whereIn('item_id', EntityDeliveryPlatform::whereIn('entity_id', $data['item_ids'])->pluck('external_item_id')->toArray())->delete();
                    ModifierGroupModifierItem::whereIn('item_id', EntityDeliveryPlatform::whereIn('entity_id', $data['item_ids'])->pluck('external_item_id')->toArray())->delete();
                    EntityDeliveryPlatform::whereIn('entity_id', $data['item_ids'])->delete();
                    ItemPrice::whereIn('entity_item_id', $data['item_ids'])->delete();
                    Item::whereIn('id', $data['item_ids'])->delete();
                    ModifierGroupModifierItem::whereIn('item_id', $data['item_ids'])->delete();
                    CommonHelper::userLog(null, ['description' => 'Deleted bulk items', 'event' => 'delete', 'subject_type' => 'item', 'subject_id' => json_encode($data['item_ids'])]);
                } else {
                    ItemCategory::whereIn('item_id', $data['item_ids'])->where('main_menu_id', $data['main_menu'])->delete();
                    ItemPrice::whereIn('entity_item_id', $data['item_ids'])->where('main_menu_id', $data['main_menu'])->delete();
                }
            });
            return $this->success('Successfully deleted the items.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function createDeliveryPlatformItem($data)
    {
        try {
            DB::transaction(function () use ($data) {
                $item = Item::where('title', $data['title'])->get();
                if (isset($data['item_id'])) {
                    $item = Item::where('id', $data['item_id'])->get();
                }

                $entityDeliveryPlatform = new EntityDeliveryPlatform;
                if (count($item) == 1) {
                    $entityDeliveryPlatform->entity_id = $item->first()->id;
                } elseif (isset($data['matching_id'])) {
                    $entityDeliveryPlatform->entity_id = $data['matching_id'];
                } else {
                    $tmpEntityDeliveryPlatform = EntityDeliveryPlatform::where('item_name', $data['title'])->whereNotNull('entity_id')->where('external_item_id', 'like', '%' . substr((string) $data['remote_id'], -5) . '%')->get();
                    if (count($tmpEntityDeliveryPlatform) > 0) {
                        $entityDeliveryPlatform->entity_id = $tmpEntityDeliveryPlatform->first()->entity_id;
                    }
                }

                if (count($item) == 1) {
                    $item = $item->first();
                    $entityDeliveryPlatform->entity_id = $item->id;
                    $itmPrice = ItemPrice::firstOrNew([
                        'main_menu_id' => (isset($data['main_menu']) ? $data['main_menu'] : 1),
                        'delivery_platform_id' => $data['delivery_platform'],
                        'entity_item_id' => $entityDeliveryPlatform->entity_id,
                    ]);
                    $itmPrice->price = ($data['price'] / 100);
                    $itmPrice->save();

                    if (is_null($item->image_url) && isset($data['image_url']) && $data['image_url'] != '') {
                        $item->image_url = $data['image_url'];
                        $item->save();
                    }
                }
                $entityDeliveryPlatform->delivery_platform_id = $data['delivery_platform'];
                $entityDeliveryPlatform->external_item_id = $data['remote_id'];
                $entityDeliveryPlatform->plu = $data['plu'];
                $entityDeliveryPlatform->price = ($data['price'] / 100);
                $entityDeliveryPlatform->item_name = $data['title'];
                $entityDeliveryPlatform->allergies = serialize((isset($data['allergies']) ? $data['allergies'] : []));
                $entityDeliveryPlatform->save();
                $this->mapModifierWithItems($entityDeliveryPlatform, $data['modifier_ids']);

                if (isset($data['price_overrides'])) {
                    foreach ($data['price_overrides'] as $key => $price) {
                        $modifier = ModifierGroupModifierItem::where('modifier_group_id', $price['id'])->where('item_id', $entityDeliveryPlatform->external_item_id)->get()->first();
                        if (!is_null($modifier)) {
                            $modifier->price = $price['price'];
                            $modifier->save();
                        }
                    }
                }
            });
            return $this->success('Successfully created delivery platform item.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function updateDeliveryPlatformItem($data, $id)
    {
        try {
            DB::transaction(function () use ($id, $data) {
                $item = Item::where('title', $data['title'])->get();
                if (isset($data['item_id'])) {
                    $item = Item::where('id', $data['item_id'])->get();
                }

                $entityDeliveryPlatform = EntityDeliveryPlatform::find($id);
                if (!is_null($entityDeliveryPlatform)) {
                    if (is_null($entityDeliveryPlatform->entity_id)) {
                        if (count($item) == 1) {
                            $entityDeliveryPlatform->entity_id = $item->first()->id;
                        } elseif (isset($data['matching_id'])) {
                            $entityDeliveryPlatform->entity_id = $data['matching_id'];
                        } else {
                            $tmpEntityDeliveryPlatform = EntityDeliveryPlatform::where('item_name', $entityDeliveryPlatform->item_name)->whereNotNull('entity_id')->where('external_item_id', 'like', '%' . substr((string) $data['remote_id'], -5) . '%')->get();
                            if (count($tmpEntityDeliveryPlatform) > 0) {
                                $entityDeliveryPlatform->entity_id = $tmpEntityDeliveryPlatform->first()->entity_id;
                            }
                        }
                    }

                    if (count($item) == 1) {
                        $item = $item->first();
                        if (is_null($item->image_url) && isset($data['image_url']) && $data['image_url'] != '') {
                            $item->image_url = $data['image_url'];
                            $item->save();
                        }
                    }

                    if (!is_null($entityDeliveryPlatform->entity_id) && isset($data['price'])) {
                        $itmPrice = ItemPrice::firstOrNew([
                            'main_menu_id' => (isset($data['main_menu']) ? $data['main_menu'] : 1),
                            'delivery_platform_id' => (isset($data['delivery_platform']) ? $data['delivery_platform'] : $entityDeliveryPlatform->delivery_platform_id),
                            'entity_item_id' => $entityDeliveryPlatform->entity_id,
                        ]);
                        $itmPrice->price = (isset($data['price']) ? ($data['price'] / 100) : $entityDeliveryPlatform->price);
                        $itmPrice->save();
                    }
                    $entityDeliveryPlatform->delivery_platform_id = (isset($data['delivery_platform']) ? $data['delivery_platform'] : $entityDeliveryPlatform->delivery_platform_id);
                    $entityDeliveryPlatform->external_item_id = (isset($data['remote_id']) ? $data['remote_id'] : $entityDeliveryPlatform->external_item_id);
                    $entityDeliveryPlatform->plu = (isset($data['plu']) ? $data['plu'] : $entityDeliveryPlatform->plu);
                    $entityDeliveryPlatform->price = (isset($data['price']) ? ($data['price'] / 100) : $entityDeliveryPlatform->price);
                    $entityDeliveryPlatform->item_name = (isset($data['title']) ? $data['title'] : $entityDeliveryPlatform->item_name);
                    $entityDeliveryPlatform->allergies = (isset($data['allergies']) ? serialize($data['allergies']) : $entityDeliveryPlatform->allergies);
                    $entityDeliveryPlatform->save();

                    $this->mapModifierWithItems($entityDeliveryPlatform, $data['modifier_ids']);

                    if (isset($data['price_overrides'])) {
                        foreach ($data['price_overrides'] as $key => $price) {
                            $modifier = ModifierGroupModifierItem::where('modifier_group_id', $price['id'])->where('item_id', $entityDeliveryPlatform->external_item_id)->get()->first();
                            if (!is_null($modifier)) {
                                $modifier->price = $price['price'];
                                $modifier->save();
                            }
                        }
                    }
                }
            });
            return $this->success('Successfully updated delivery platform item.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function mapModifierWithItems($entityDeliveryPlatform, $modifierIds)
    {
        try {
            DB::transaction(function () use ($entityDeliveryPlatform, $modifierIds) {
                $modIds = [];
                foreach ($modifierIds as $key => $value) {
                    $modifierGroup = ModifierGroup::where('remote_id', $value)->get()->first();
                    if (!is_null($modifierGroup)) {
                        $value = $modifierGroup->id;
                    }
                    $modItm = ModifierGroupItem::firstOrNew([
                        'modifier_group_id' => $value,
                        'item_id' => $entityDeliveryPlatform->external_item_id,
                    ]);
                    $modItm->save();
                    $modIds[$value][] = $modItm->id;
                }
                foreach ($modIds as $key => $modIdList) {
                    ModifierGroupItem::whereNotIn('id', $modIdList)->where('modifier_group_id', $key)->where('item_id', (string) $entityDeliveryPlatform->external_item_id)->delete();
                }
            });
            return $this->success('Successfully mapped the items.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function getPlatformItems()
    {
        try {
            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $deliveryPlatforms = (json_decode($response->getBody()))->data;
            $deliveryPlatformList = [];
            $platformItems = [];
            foreach ($deliveryPlatforms as $pkey => $deliveryPlatform) {
                $itemList = [];
                $items = EntityDeliveryPlatform::where('delivery_platform_id', $deliveryPlatform->id)->get();
                foreach ($items as $key => $item) {
                    $subText = 'Price: ' . $item->price . ' |';
                    if (CommonHelper::isModifier($item->external_item_id)) {
                        $itemNames = ModifierGroupModifierItem::where('item_id', $item->external_item_id)->get();
                        $names = '(';
                        foreach ($itemNames as $mkey => $it) {
                            if (!is_null($it->modifier)) {
                                $names .= $it->modifier->title;
                                if ($mkey != count($itemNames) - 1) {
                                    $names .= ', ';
                                }
                            }
                        }
                        $names .= ')';
                        $subText .= ' Modifier ' . $names . ' |';
                    }
                    if (count($item->modifiers) > 0) {
                        $modifiers = $item->modifiers;
                        $names = '(';
                        foreach ($modifiers as $mkey => $it) {
                            if (!is_null($it->modifier)) {
                                $names .= $it->modifier->title;
                                if ($mkey != count($modifiers) - 1) {
                                    $names .= ', ';
                                }
                            }
                        }
                        $names .= ')';
                        $subText .= ' Has modifiers ' . $names . ' |';
                    }
                    $itemList[] = ['id' => $item->id, 'name' => $item->item_name, 'subText' => substr(substr($subText, 0, -2), 0, 60)];
                }
                $platformItems[$deliveryPlatform->id] = $itemList;
            }
            return $this->success('List', $platformItems);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function savePlatformItemMapping($data)
    {
        try {
            DB::transaction(function () use ($data) {
                $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
                $deliveryPlatforms = (json_decode($response->getBody()))->data;
                $deliveryPlatformList = [];
                $item = Item::find($data['item_id']);
                foreach ($deliveryPlatforms as $key => $deliveryPlatform) {
                    if ($item->source == 'POS') {
                        if (isset($data['platform_item_' . $deliveryPlatform->id]) && $data['platform_item_' . $deliveryPlatform->id] != '') {
                            $entityItem = EntityDeliveryPlatform::find($data['platform_item_' . $deliveryPlatform->id]);
                            EntityDeliveryPlatform::where('entity_id', $data['item_id'])->where('delivery_platform_id', $deliveryPlatform->id)->update(['entity_id' => null]);
                            EntityDeliveryPlatform::where('id', $data['platform_item_' . $deliveryPlatform->id])->update(['entity_id' => $data['item_id']]);
                            if (!is_null($entityItem) && !is_null($entityItem->entity_id)) {
                                //ItemCategory::where('item_id', $data['item_id'])->update(['item_id' => null]);
                                ItemCategory::where('item_id', $data['item_id'])->delete();
                                ItemCategory::where('item_id', $entityItem->entity_id)->update(['item_id' => $data['item_id']]);
                                ItemPrice::where('entity_item_id', $data['item_id'])->where('delivery_platform_id', $deliveryPlatform->id)->update(['entity_item_id' => null]);
                                ItemPrice::where('entity_item_id', $entityItem->entity_id)->where('delivery_platform_id', $deliveryPlatform->id)->update(['entity_item_id' => $data['item_id']]);
                                $mainMenus = MainMenu::all();

                                foreach ($mainMenus as $mkey => $mainMenu) {
                                    $mainMenuItems = unserialize($mainMenu->item_ids);
                                    if ($mainMenuItems != false && is_array($mainMenuItems) && in_array($entityItem->entity_id, $mainMenuItems)) {
                                        if (($ikey = array_search($entityItem->entity_id, $mainMenuItems)) !== false) {
                                            unset($mainMenuItems[$ikey]);
                                        }
                                        $mainMenuItems[] = $data['item_id'];
                                        $mainMenu->item_ids = serialize($mainMenuItems);
                                        $mainMenu->save();
                                    }
                                }
                            }
                        }
                    } elseif (isset($data['platform_item_' . $deliveryPlatform->id])) {
                        EntityDeliveryPlatform::where('entity_id', $data['item_id'])->where('delivery_platform_id', $deliveryPlatform->id)->update(['entity_id' => null]);
                        EntityDeliveryPlatform::where('id', $data['platform_item_' . $deliveryPlatform->id])->update(['entity_id' => $data['item_id']]);
                    }
                }
            });
            return $this->success('Mapping success');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function getMenuItemDetails($id, $main_menu)
    {
        try {
            $item = Item::find($id);
            if (is_null($item)) {
                return $this->notFound('Item not found');
            }
            $categoryIds = [];
            foreach ($item->categories as $key => $category) {
                $categoryIds[] = $category->id;
            }
            $item->category_ids = $categoryIds;
            if (!is_null($main_menu)) {
                $itemPrice = $item->prices->where('main_menu_id', $main_menu);
                if (count($itemPrice) > 0) {
                    $item->price = $itemPrice->first()->price;
                }
            }
            return $this->success('Menu item.', $item);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function getItemsByQuery($data)
    {
        try {
            $query = (isset($data['query']) ? $data['query'] : '');

            $results = DB::table('item')
                ->leftJoin('item_category', 'item.id', '=', 'item_category.item_id')
                ->leftJoin('category', 'category.id', '=', 'item_category.category_id')
                ->leftJoin('category_menu', 'category.id', '=', 'category_menu.category_id')
                ->leftJoin('menu', 'menu.id', '=', 'category_menu.menu_id')
                ->select(['item.title as itemTitle', 'item.id as itemId', 'category.id as categoryId', 'menu.title as menuTitle', 'menu.id as menuId', 'item.price as itemPrice'])
                ->where(function ($searchQuery) use ($query) {
                    $searchQuery->where('item.title', 'like', '%' . $query . '%');
                    $searchQuery->orWhere('item.price', 'like', '%' . $query . '%');
                });
            $main_menu = (isset($data['main_menu']) ? MainMenu::find($data['main_menu']) : null);
            $allowedModifierIds = [];
            if (!is_null($main_menu)) {
                $allowedModifierIds = $main_menu->modifiers->pluck('id')->toArray();
            }
            if (isset($data['menu_id']) && $data['menu_id'] != '') {
                $results = $results->where('menu.id', $data['menu_id']);
            }
            if (isset($data['category_id']) && $data['category_id'] != '') {
                $results = $results->where('category.id', $data['category_id']);
            }
            $results = $results->pluck('itemId');
            $masterOutletDeliveryIds = [];
            $platformResponse = $this->request_handle_service->getRequst(null, '/api/v1/admin/platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platforms = (json_decode($platformResponse->getBody()))->data;
            $platformList = [];
            foreach ($platforms as $key => $platform) {
                $platformList[$platform->id] = $platform->logo;
            }

            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $deliveryPlatforms = (json_decode($response->getBody()))->data;
            $deliveryPlatformList = [];
            $deliveryPlatformIds = [];
            foreach ($deliveryPlatforms as $key => $deliveryPlatform) {
                if (!is_null($main_menu) && $deliveryPlatform->outlet_id == $main_menu->master_outlet) {
                    $masterOutletDeliveryIds[] = $deliveryPlatform->id;
                }
                $deliveryPlatformList[$deliveryPlatform->id] = ['id' => $deliveryPlatform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $deliveryPlatform->name))), 'logo' => $platformList[$deliveryPlatform->platform_id]];
                $deliveryPlatformIds[] = $deliveryPlatform->id;
            }

            $processedItemList = [];
            $itemList = Item::whereIn('id', $results)->get()->fresh('entityDeliveryPlatform', 'categories');

            foreach ($itemList as $key => $item) {
                $item->is_valid_image = (is_null($item->image_url) ? true : (in_array(strtoupper(pathinfo($item->image_url, PATHINFO_EXTENSION)), ['JPEG', 'JPG', 'PNG']) ? true : false));
                $platform_urls = [];
                $item->masterEntityDeliveryPlatforms = $item->entityDeliveryPlatform->whereIn('delivery_platform_id', $masterOutletDeliveryIds);
                $item->categoryList = $item->categoriesByMainMenu($data['main_menu']);
                $itemPrice = $item->prices->where('main_menu_id', $data['main_menu'])->where('delivery_platform_id', null);
                $availablePlatformIds = $item->prices->where('main_menu_id', (is_null($main_menu) ? null : $main_menu->id))->whereNotNull('delivery_platform_id')->pluck('delivery_platform_id')->toArray();
                $item->availablePlatformIds = $availablePlatformIds;
                if (count($itemPrice) > 0) {
                    $item->price = $itemPrice->first()->price;
                }
                $modifiers = null;
                $allergies = [];
                $tmpPlatforms = [];
                $is_modifier = false;
                $entityDeliveryPlatforms = [];
                $masterEntityDeliveryPlatforms = [];
                $modifierList = [];
                foreach ($item->masterEntityDeliveryPlatforms as $key => $masterEntity) {
                    foreach ($masterEntity->modifiers->whereIn('modifier_group_id', $allowedModifierIds) as $mkey => $mod) {
                        $modifierList[$mod->modifier_group_id] = $mod;
                    }
                    $masterEntity->allergies = (unserialize($masterEntity->allergies) == false ? [] : unserialize($masterEntity->allergies));
                    if (count($allergies) < count($masterEntity->allergies)) {
                        $allergies = $masterEntity->allergies;
                    }
                    if (!$is_modifier) {
                        $is_modifier = CommonHelper::isModifier($masterEntity->external_item_id);
                    }
                    $tmpPrice = $item->prices->where('main_menu_id', (is_null($main_menu) ? null : $main_menu->id))->where('delivery_platform_id', $masterEntity->delivery_platform_id);
                    if (count($tmpPrice) > 0) {
                        $masterEntity->price = $tmpPrice->first()->price;
                    }
                    if (in_array($masterEntity->delivery_platform_id, $availablePlatformIds)) {
                        $masterEntityDeliveryPlatforms[] = $masterEntity;
                        if (!in_array($masterEntity->delivery_platform_id, $tmpPlatforms) && !is_null($main_menu)) {
                            $platform_urls[] = $deliveryPlatformList[$masterEntity->delivery_platform_id];
                            $tmpPlatforms[] = $masterEntity->delivery_platform_id;
                        }
                    }
                }
                $modifiers = array_values($modifierList);
                $item->masterEntityDeliveryPlatforms = $masterEntityDeliveryPlatforms;
                foreach ($item->entityDeliveryPlatform as $key1 => $entity) {
                    if (in_array($entity->delivery_platform_id, $deliveryPlatformIds)) {
                        $tmpPrice = $item->prices->where('main_menu_id', (is_null($main_menu) ? null : $main_menu->id))->where('delivery_platform_id', $entity->delivery_platform_id);
                        if (count($tmpPrice) > 0) {
                            $entity->price = $tmpPrice->first()->price;
                            if (in_array($entity->delivery_platform_id, $availablePlatformIds)) {
                                $entityDeliveryPlatforms[] = $entity;
                            }
                        }
                    }
                }
                if (!is_null($main_menu)) {
                    $item->entityDeliveryPlatform = $entityDeliveryPlatforms;
                }
                $item->modifiers = $modifiers;
                $item->allergies = $allergies;
                $item->platform_urls = $platform_urls;
                $item->is_modifier = $is_modifier;

                if (isset($data['category_id']) && $data['category_id'] != '') {
                    if (in_array($data['category_id'], $item->categoryList->pluck('id')->toArray())) {
                        $processedItemList[] = $item;
                    }
                } else {
                    $processedItemList[] = $item;
                }
            }

            return $this->success('Search result', $processedItemList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function itemTaxStore($data, $item_id)
    {
        foreach ($data as $key => $value) {
            $item_tax = ItemTax::firstOrNew([
                'tax_id' => $value,
                'item_id' => $item_id,
            ]);
            $item_tax->save();
        }
    }

    public function itemModifierStore($data, $item_id, $platform)
    {
        foreach ($data as $key => $value) {
            $modifierGroup = ModifierGroup::find($value);
            if (!is_null($modifierGroup)) {
                $item_mod = ModifierGroupItem::firstOrNew([
                    'modifier_group_id' => $modifierGroup->id,
                    'item_id' => $item_id,
                ]);
                $item_mod->platform = $platform;
                $item_mod->save();
            }
        }
    }

    public function getUnmatchedItems($shop_id)
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
            $shopDeliveryPlatformIds = [];
            foreach ($deliveryPlatforms as $key => $deliveryPlatform) {
                if ($deliveryPlatform->outlet_id == $shop_id) {
                    $shopDeliveryPlatformIds[] = $deliveryPlatform->id;
                }
                if (in_array($deliveryPlatform->platform_id, Config::get('common.fetchable_platforms'))) {
                    $deliveryPlatformList[$deliveryPlatform->id] = ['id' => $deliveryPlatform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $deliveryPlatform->name))), 'logo' => $platformList[$deliveryPlatform->platform_id]];
                }
            }
            if (is_null($shop_id)) {
                $items = EntityDeliveryPlatform::whereNull('entity_id')->get();
            } else {
                $items = EntityDeliveryPlatform::whereIn('delivery_platform_id', $shopDeliveryPlatformIds)->whereNull('entity_id')->get();
            }
            $itemList = [];

            foreach ($items as $key => $item) {
                if (isset($deliveryPlatformList[$item->delivery_platform_id])) {
                    $itemList[] = ['item_name' => $item->item_name, 'delivery_platform' => $deliveryPlatformList[$item->delivery_platform_id], 'entity_id' => $item->id];
                }
            }
            return $this->success('Item List', $itemList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function createItemForRemaining($id, $mainMenu)
    {
        try {
            DB::transaction(function () use ($id, $mainMenu) {
                $entityIds = [];
                if ($id == 'ALL') {
                    $entityIds = EntityDeliveryPlatform::whereNull('entity_id')->get()->pluck('id')->toArray();
                } else {
                    $entityIds = [$id];
                }
                $entityDeliveryPlatformItems = EntityDeliveryPlatform::whereIn('id', $entityIds)->get();
                foreach ($entityDeliveryPlatformItems as $key => $entityDeliveryPlatformItem) {
                    $itemSuccess = false;
                    $itemArray = ['title' => $entityDeliveryPlatformItem->item_name, 'tax' => 0, 'price' => $entityDeliveryPlatformItem->price, 'status' => 1, 'contains_alcohol' => false];
                    if (!is_null($mainMenu)) {
                        $itemArray['main_menu'] = $mainMenu;
                    }
                    $existingEntityItem = EntityDeliveryPlatform::where('item_name', $entityDeliveryPlatformItem->item_name)->whereNotNull('entity_id')->get();
                    if (count($existingEntityItem) > 0) {
                        $existingEntityItem = $existingEntityItem->first();
                        $itemSuccess = true;
                    } else {
                        $itemResponse = $this->store($itemArray);
                        if ($itemResponse->getStatusCode() == 200) {
                            $existingEntityItem = (json_decode($itemResponse->getContent()))->data;
                            $itemSuccess = true;
                        }
                    }
                    if ($itemSuccess) {
                        $entityDeliveryPlatformItem->entity_id = $existingEntityItem->id;
                        $entityDeliveryPlatformItem->save();
                        $itmPrice = ItemPrice::firstOrNew([
                            'main_menu_id' => $mainMenu,
                            'entity_item_id' => $existingEntityItem->id,
                            'delivery_platform_id' => $entityDeliveryPlatformItem->delivery_platform_id,
                        ]);
                        $itmPrice->price = $entityDeliveryPlatformItem->price;
                        $itmPrice->save();
                    }
                }
            });
            return $this->success('Item created');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function storeOrUpdatePosItem($data)
    {
        try {
            DB::transaction(function () use ($data) {
                if (is_null($data['variant_id'])) {
                    $posItem = PosItem::firstOrNew([
                        'pos_id' => $data['pos_id'],
                        'pos_item_id' => $data['pos_item_id'],
                    ]);
                    $posItem->variant_id = $data['variant_id'];
                } else {
                    $posItem = PosItem::firstOrNew([
                        'pos_id' => $data['pos_id'],
                        'variant_id' => $data['variant_id'],
                    ]);
                    $posItem->pos_item_id = $data['pos_item_id'];
                }
                $posItem->title = $data['title'];
                $posItem->shop_id = $data['shop_id'];
                $posItem->item_id_id = $data['item_id_id'];
                $posItem->handle = $data['handle'];
                $posItem->reference_id = $data['reference_id'];
                $posItem->track_stock = $data['track_stock'];
                $posItem->sold_by_weight = $data['sold_by_weight'];
                $posItem->is_composite = $data['is_composite'];
                $posItem->use_production = $data['use_production'];
                $posItem->form = $data['form'];
                $posItem->color = $data['color'];
                $posItem->available_for_sale = $data['available_for_sale'];
                $posItem->store_id = $data['store_id'];
                $posItem->cost = $data['cost'];
                $posItem->reference_variant_id = $data['reference_variant_id'];
                $posItem->barcode = $data['barcode'];
                $posItem->purchase_cost = $data['purchase_cost'];
                $posItem->default_pricing_type = $data['default_pricing_type'];
                $posItem->default_price = $data['default_price'];
                $posItem->save();

                if (isset($data['loyverse_item'])) {
                    foreach ($data['stores'] as $key => $value) {
                        $variat_store = new VariantStore;
                        $variat_store->store_id = $value->store_id;
                        $variat_store->pricing_type = $value->pricing_type;
                        $variat_store->variant_id = $posItem->id;
                        $variat_store->price = $value->price;
                        $variat_store->available_for_sale = $value->available_for_sale;
                        $variat_store->optimal_stock = $value->optimal_stock;
                        $variat_store->low_stock = $value->low_stock;
                        $variat_store->save();
                    }

                    if (isset($data['modifier_ids'])) {
                        $this->itemModifierStore($data['modifier_ids'], $posItem->id, 'LOYVERSE');
                    }

                    if (isset($data['tax_ids'])) {
                        $this->itemTaxStore($data['tax_ids'], $posItem->id);
                    }
                }
            });
            return $this->success('Item');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function snoozeItem($itemId, $shopId, $date = '2243-10-01 00:00:00')
    {
        try {
            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $deliveryPlatforms = (json_decode($response->getBody()))->data;
            $platforms = collect($deliveryPlatforms)->where('outlet_id', $shopId)->whereIn('platform_id', Config::get('common.fetchable_platforms'));
            $platformIds = $platforms->pluck('id')->toArray();
            $entityItems = EntityDeliveryPlatform::where('entity_id', $itemId)->whereIn('delivery_platform_id', $platformIds)->get();
            $unsnoozedItems = [];
            foreach ($entityItems as $key => $entityItem) {
                if ($entityItem->available) {
                    $entityItem->available_from = DateTimeUtility::getDateTimeFormat($date, 'Y-m-d H:i:s');
                } else {
                    $unsnoozedItems[] = $entityItem->external_item_id;
                    $entityItem->available_from = null;
                }
                $entityItem->available = !$entityItem->available;
                $entityItem->save();
            }

            $webShopPlatforms = collect($deliveryPlatforms)->where('outlet_id', $shopId)->where('platform_id', 6);
            $webShopPlatformIds = $webShopPlatforms->pluck('id')->toArray();
            $webShopEntityItems = EntityDeliveryPlatform::whereIn('entity_id', $itemId)->whereIn('delivery_platform_id', $webShopPlatformIds)->get();
            foreach ($webShopEntityItems as $WSkey => $webShopEntityItem) {
                if ($webShopEntityItem->available) {
                    $webShopEntityItem->available_from = DateTimeUtility::getDateTimeFormat($date, 'Y-m-d H:i:s');
                } else {
                    $unsnoozedItems[] = $webShopEntityItem->external_item_id;
                    $webShopEntityItem->available_from = null;
                }
                $webShopEntityItem->available = !$webShopEntityItem->available;
                $webShopEntityItem->save();
            }

            $itemStatuses = [];
            foreach ($platforms as $key => $platform) {
                if ($platform->platform_id == 1) {
                    $itemStatuses[$platform->id] = EntityDeliveryPlatform::where('available', 0)->where('delivery_platform_id', $platform->id)->get()->pluck('external_item_id')->toArray();
                } elseif ($platform->platform_id == 2) {
                    $entity = EntityDeliveryPlatform::where('entity_id', $itemId)->where('delivery_platform_id', $platform->id)->get()->first();
                    $itemStatuses[$platform->id][] = ['item_id' => $entity->external_item_id, 'available' => $entity->available, 'available_from' => $entity->available_from];
                }
            }
            $unsnoozedItems = array_unique($unsnoozedItems);
            $unavailableResponse = $this->request_handle_service->postRequst(['delivery_platform' => $platformIds, 'items' => $itemStatuses, 'unsnoozed_items' => $unsnoozedItems], '/api/v1/remote/make-item-unavailable', 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            if ($unavailableResponse->getStatusCode() == 200) {
                // $responseData = (json_decode($unavailableResponse->getBody()))->data;
                // foreach ($responseData as $key => $platformData) {
                //     if ($platformData->original->code == 500) {
                //         return $this->error($platformData->original->message);
                //     }
                // }
                return $this->success('snoozed Items', (json_decode($unavailableResponse->getBody()))->data);
            } else {
                return $this->error('snoozed Items', (json_decode($unavailableResponse->getBody()))->data);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function snoozeItems($shopId, $data)
    {
        try {
            $availableFrom = null;
            $availableFromInput = (isset($data['available_from']) ? $data['available_from'] : 'forever');
            switch ($availableFromInput) {
                case '1_hour':
                    $availableFrom = DateTimeUtility::addRemoveDaysFromDate('Now', '+ 60 minutes', 'Y-m-d H:i:s');
                    break;
                case '3_hour':
                    $availableFrom = DateTimeUtility::addRemoveDaysFromDate('Now', '+ 180 minutes', 'Y-m-d H:i:s');
                    break;
                case 'next_day':
                    $availableFrom = DateTimeUtility::getDateTimeFormat('tomorrow 4 am', 'Y-m-d H:i:s');
                    break;
                case '1_week':
                    $availableFrom = DateTimeUtility::addRemoveDaysFromDate('Now', '+ 7 days', 'Y-m-d 04:00:00');
                    break;
                case 'forever':
                    $availableFrom = DateTimeUtility::getDateTimeFormat('2243-01-01', 'Y-m-d H:i:s');
                    break;
                default:
                    $availableFrom = DateTimeUtility::getDateTimeFormat('2243-01-01', 'Y-m-d H:i:s');
                    break;
            }
            $itemId = $data['item_ids'];
            // dd($itemId);
            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $deliveryPlatforms = (json_decode($response->getBody()))->data;
            $platforms = collect($deliveryPlatforms)->where('outlet_id', $shopId)->whereIn('platform_id', Config::get('common.fetchable_platforms'));
            $platformIds = $platforms->pluck('id')->toArray();
            $entityItems = EntityDeliveryPlatform::whereIn('entity_id', $itemId)->whereIn('delivery_platform_id', $platformIds)->get();
            $snoozeStatus = (isset($data['snooze_status']) ? $data['snooze_status'] : null);
            $unsnoozedItems = [];
            foreach ($entityItems as $key => $entityItem) {
                $toStatus = (is_null($snoozeStatus) ? (!$entityItem->available) : $snoozeStatus);
                if ($toStatus) {
                    $unsnoozedItems[] = $entityItem->external_item_id;
                    $entityItem->available_from = null;
                } else {
                    $entityItem->available_from = $availableFrom;
                }
                $entityItem->available = $toStatus;
                $entityItem->save();
            }
            $webShopPlatforms = collect($deliveryPlatforms)->where('outlet_id', $shopId)->whereIn('platform_id', [6, 8]);
            $webShopPlatformIds = $webShopPlatforms->pluck('id')->toArray();
            $webShopEntityItems = EntityDeliveryPlatform::whereIn('entity_id', $itemId)->whereIn('delivery_platform_id', $webShopPlatformIds)->get();
            foreach ($webShopEntityItems as $WSkey => $webShopEntityItem) {
                $toStatus = (is_null($snoozeStatus) ? (!$webShopEntityItem->available) : $snoozeStatus);
                if ($toStatus) {
                    $unsnoozedItems[] = $webShopEntityItem->external_item_id;
                    $webShopEntityItem->available_from = null;
                } else {
                    $webShopEntityItem->available_from = $availableFrom;
                }
                $webShopEntityItem->available = $toStatus;
                $webShopEntityItem->save();
            }
            if ($webShopEntityItems->count() > 0) {
                UpdatePosWebshopMenu::dispatch(['mainMenuId' => null, 'shopId' => $shopId, 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
            }

            $itemStatuses = [];
            foreach ($platforms as $key => $platform) {
                if ($platform->platform_id == 1) {
                    $itemStatuses[$platform->id] = EntityDeliveryPlatform::where('available', 0)->where('delivery_platform_id', $platform->id)->get()->pluck('external_item_id')->toArray();
                } elseif ($platform->platform_id == 2) {
                    $entity = EntityDeliveryPlatform::whereIn('entity_id', $itemId)->where('delivery_platform_id', $platform->id)->get();
                    foreach ($entity as $keyE => $prod) {
                        $itemStatuses[$platform->id][] = ['item_id' => $prod->external_item_id, 'available' => $prod->available, 'available_from' => $prod->available_from];
                    }
                }
            }
            if ($webShopEntityItems->count() == 0) {
                // UpdateSnoozeItemList::dispatch(['shopIds' => [$shopId], 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
            }
            $unsnoozedItems = array_unique($unsnoozedItems);
            $unavailableResponse = $this->request_handle_service->postRequst(['delivery_platform' => $platformIds, 'items' => $itemStatuses, 'unsnoozed_items' => $unsnoozedItems, 'item_ids' => $itemId], '/api/v1/remote/make-item-unavailable', 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            if ($unavailableResponse->getStatusCode() == 200) {
                CommonHelper::userLog(null, ['description' => (isset($snoozeStatus) && $snoozeStatus == 0 ? 'Snoozed' : 'Unsnoozed') . ' items', 'event' => (isset($snoozeStatus) && $snoozeStatus == 0 ? 'snooze' : 'unsnooze'), 'subject_type' => 'item', 'subject_id' => json_encode($itemId)]);
                return $this->success('snoozed Items', (json_decode($unavailableResponse->getBody()))->data);
            } else {
                return $this->error('snoozed Items', (json_decode($unavailableResponse->getBody()))->data);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function deleteEntityItems($id)
    {
        try {
            EntityDeliveryPlatform::where('delivery_platform_id', $id)->delete();
            return $this->success('Deleted Items');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function updateProductDiscountPrice($data)
    {
        try {
            DB::transaction(function () use ($data) {
                if (isset($data['platform_ids'])) {
                    $platform_ids = $data['platform_ids'];
                } else {
                    $platform_ids = [6, 8];
                }
                $dp = DB::table('delivery_platform')->where('outlet_id', $data['shopID'])->whereIn('platform_id', $platform_ids)->where('webshop_brand_id', $data['webshopBrandID'])->pluck('id')->toArray();
                if (!is_null($data['product']['title'])) {
                    $entityItem = EntityDeliveryPlatform::whereIn('delivery_platform_id', $dp)->where('entity_id', $data['product']['id'])->update(['item_name' => $data['product']['title']]);
                }
                foreach ($dp as $key => $dpId) {
                    $itmPrice = ItemPrice::firstOrNew([
                        'main_menu_id' => $data['menuID'],
                        'entity_item_id' => $data['product']['id'],
                        'delivery_platform_id' => $dpId,
                    ]);
                    $itmPrice->price = $data['product']['price'];
                    $itmPrice->sale_price = $data['product']['sale_price'];
                    $itmPrice->discount_amount = (isset($data['product']['amount'])?$data['product']['amount']:0);
                    $itmPrice->tax_percentage = (isset($data['product']['tax_percentage'])?$data['product']['tax_percentage']:0);
                    $itmPrice->discount_type = (isset($data['product']['discount_type'])?$data['product']['discount_type']:'percentage');
                    $itmPrice->is_sale = $data['product']['is_sale'];
                    $itmPrice->has_bogo_offer = (isset($data['product']['has_bogo_offer'])?$data['product']['has_bogo_offer']:0);
                    $itmPrice->bogo_category = (isset($data['product']['bogo_category'])?$data['product']['bogo_category']:null);
                    $itmPrice->save();

                    if ($itmPrice->is_sale) {
                        $category = Category::where('title', 'Offers')->where('status', 1)->get();
                        $catId = null;
                        if (count($category) > 0) {
                            $catId = $category->first()->id;
                        }
                        if (is_null($catId)) {
                            $category = Category::where('title', 'Offers')->get();
                            if (count($category) > 0) {
                                $catId = $category->first()->id;
                            }
                        }

                        if (is_null($catId)) {
                            $categoryService = new CategoryService;
                            $response = $categoryService->store(['title' => 'Offers', 'status' => 1]);
                            if ($response->getStatusCode() == 200) {
                                Category::where('title', 'Offers')->update(['priority' => 0]);
                                $category = Category::where('title', 'Offers')->where('status', 1)->get();
                                $catId = $category->first()->id;
                            }
                        }

                        if (!is_null($catId)) {
                            $itmCat = ItemCategory::firstOrNew([
                                'item_id' => $data['product']['id'],
                                'main_menu_id' => $data['menuID'],
                                'category_id' => $catId,
                            ]);
                            $itmCat->save();
                        }
                    }
                }
                UpdatePosWebshopMenu::dispatch(['mainMenuId' => /*$data['menuID']*/null, 'shopId' => $data['shopID'], 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
                $webshop_brand = DB::table('webshop_brand')->where('id', $data['webshopBrandID'])->first();
                CommonHelper::userLog(null, ['webshop_brand_id' => $data['webshopBrandID'], 'webshop_brand_name' => $webshop_brand->brand_name, 'shop_id' => $data['shopID'], 'description' => 'Updated item discount price', 'event' => 'update', 'subject_type' => 'item', 'subject_id' => $data['product']['id']]);
            });
            return $this->success('Updated the item details.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function updateItemPrice($data, $id)
    {
        try {
            $delivery_platform = DB::table('delivery_platform')
                ->where('outlet_id', $data['shopID'])
                ->whereIn('platform_id', [6, 8])
                ->where('webshop_brand_id', $data['webshopBrandID'])
                ->pluck('id')
                ->toArray();

            if (count($delivery_platform) == 0) {
                return $this->notFound('No available webshop or table order delivery platforms for this shop id');
            }

            $item_price = ItemPrice::whereIn('delivery_platform_id', $delivery_platform)
                ->where('main_menu_id', $data['menuID'])
                ->where('entity_item_id', $id)
                ->update(['price' => $data['price']]);

            UpdatePosWebshopMenu::dispatch(['mainMenuId' => /*$data['menuID']*/null, 'shopId' => $data['shopID'], 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
            $webshop_brand = DB::table('webshop_brand')->where('id', $data['webshopBrandID'])->first();
            CommonHelper::userLog(null, ['webshop_brand_id' => $data['webshopBrandID'], 'webshop_brand_name' => $webshop_brand->brand_name, 'shop_id' => $data['shopID'], 'description' => 'Updated item price', 'event' => 'update', 'subject_type' => 'item', 'subject_id' => $id]);
            return $this->success('Webshop and table order item price updated');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function addOrRemoveCustomerFavouriteItem($data)
    {
        try {
            $message = null;
            $user = Auth::guard('customers')->user();
            if ($user) {
                DB::transaction(function () use ($data, &$message, $user) {
                    $fav_item = CustomerFavouriteItem::where('customer_id', $user->id)
                        ->where('item_id', $data['item_id'])
                        ->first();
                    if (is_null($fav_item)) {
                        $fav_item = new CustomerFavouriteItem;
                        $fav_item->customer_id = $user->id;
                        $fav_item->item_id = $data['item_id'];
                        $fav_item->save();
                        $message = 'Successfully added to favourite items';
                    } else {
                        $fav_item->delete();
                        $message = 'Successfully removed from favourite items';
                    }
                });
                return $this->success($message);
            } else {
                return $this->error('Could not proceed');
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getCustomerFavouriteOrPurchasedItems($menu_id, $dp = null, $active = false, $query = '', $outlet = null, $type)
    {
        try {
            $user = Auth::guard('customers')->user();
            if ($user) {
                if (is_null($type)) {
                    return $this->error('Select favourite or purchased items');
                }
                $customer_itemIds = null;
                $message = null;
                $items_arr = [];
                $allowedItemIds = [];
                $itemIds = null;

                $mainMenu = MainMenu::find($menu_id);
                if (is_null($mainMenu)) {
                    return $this->notFound('Main menu not found');
                }

                if (!is_null($dp)) {
                    $deliveryPlatform = DB::table('delivery_platform')->where('id', $dp)->get()->first();
                    $shop = Shop::find($deliveryPlatform->outlet_id);
                    $day = strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Now', 'l', $shop->timezone));
                    $time = strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Now', 'H:i:s', $shop->timezone));

                    $submenuIds = $mainMenu->menus->pluck('id')->toArray();

                    // Build base query conditions
                    $baseConditions = [
                        'main_menu_id' => $menu_id,
                        'delivery_platform_id' => $dp,
                        'status' => $active,
                        'outlet_id' => $deliveryPlatform->outlet_id
                    ];

                    $webshopMenu = $this->menu_service->findWebshopMenu($baseConditions, $submenuIds, $day, $time, $shop->timezone);

                    // $webshopMenu = WebshopMenu::where('main_menu_id', $menu_id)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp)->where('status', $active)->where('outlet_id', $deliveryPlatform->outlet_id)->where('day', $day)->whereTime('from', '<=', $time)->whereTime('to', '>=', $time)->orderBy('id', 'DESC')->get();

                    // if (count($webshopMenu)==0) {
                    //     $previousDay = strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Yesterday', 'l', $shop->timezone));
                    //     $webshopMenu = WebshopMenu::where('main_menu_id', $menu_id)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp)->where('status', $active)->where('outlet_id', $deliveryPlatform->outlet_id)->where('day', $previousDay)/*->whereTime('from', '>=', $time)*/->whereTime('to', '>=', $time)->orderBy('id', 'DESC')->get();

                    //     if (count($webshopMenu)==0 || ($webshopMenu->first()->from < $webshopMenu->first()->to)) {
                    //         $webshopMenu = WebshopMenu::where('main_menu_id', $menu_id)->whereIn('submenu_id', $mainMenu->menus->pluck('id')->toArray())->where('delivery_platform_id', $dp)->where('status', $active)->where('outlet_id', $deliveryPlatform->outlet_id)/*->where('day', $day)->whereTime('to', '>=', $time)*/->orderBy('id', 'DESC')->get();
                    //     }
                    // }

                    $allowedMenuIds = [];
                    if (count($webshopMenu) > 0) {
                        $webshopMenu = $webshopMenu->first();
                        $allowedMenuIds = [$webshopMenu->submenu_id];
                        $menu = Menu::find($webshopMenu->submenu_id);
                        if(!is_null($menu)) {
                            $itemIds = unserialize($menu->item_ids);
                        }
                    } else {
                        $allowedMenuIds = MainMenuMenu::where('main_menu_id', $menu_id)->pluck('menu_id')->toArray();
                    }
                    $allowedCategoryIds = CategoryMenu::where('main_menu_id', $menu_id)->whereIn('menu_id', $allowedMenuIds)->pluck('category_id')->toArray();
                    $allowedItemIds = ItemCategory::whereIn('category_id', $allowedCategoryIds)->where('main_menu_id', $menu_id)->pluck('item_id')->toArray();
                }

                if ($type == 'FAVOURITE' || $type == 'FAVORITE') {
                    $customer_itemIds = CustomerFavouriteItem::whereIn('item_id', $allowedItemIds)->where('customer_id', $user->id)->orderBy('created_at', 'DESC')->limit(6)->pluck('item_id')->toArray();
                    $message = 'Successfully retrieved the customer favourite items';
                } elseif ($type == 'PURCHASED') {
                    $customer_item_count = DB::table('order')->select('order_items.item_id', DB::raw('COUNT("order_items.item_id") AS item_count'))
                        ->join('order_items', 'order_items.order_id', '=', 'order.id')
                        ->where('order.customer_id', $user->id)
                        ->whereIn('order_items.item_id', $allowedItemIds)
                        ->groupBy('order_items.item_id')
                        ->orderBy('item_count', 'DESC')
                        ->limit(6)
                        ->get();

                    $customer_itemIds = $customer_item_count->pluck('item_id')->toArray();
                    $message = 'Successfully retrieved the customer purchased items';
                }

                if (!$itemIds) {
                    $itemIds = unserialize($mainMenu->item_ids);
                    if (!$itemIds) {
                        $itemIds = [];
                    }
                }
                $item_in_menu = array_intersect($customer_itemIds, $itemIds);
                if ($query == '') {
                    $items = Item::whereIn('id', $item_in_menu)->where('status', 1)->get();
                } else {
                    $items = Item::whereIn('id', $item_in_menu)->where('title', 'LIKE', '%' . $query . '%')->where('status', 1)->get();
                }
                $modifierGroupList = CommonHelper::getModifierGroups($dp, $mainMenu->id);
                $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
                $platforms = (json_decode($response->getBody()))->data;
                $platformList = [];
                $dpIds = [];
                foreach ($platforms as $key => $platform) {
                    $platformList[$platform->id] = ['id' => $platform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $platform->name))), 'logo' => $platform->logo];
                    if (!is_null($outlet) && ($platform->outlet_id == $outlet)) {
                        $dpIds[] = $platform->id;
                    }
                }
                foreach ($items as $key => $item) {
                    $item->availability = 0;
                    $item->snooze_available = 1;
                    $item->modifiers = [];

                    if (is_null($dp)) {
                        $item->priceList = $item->prices->where('main_menu_id', $menu_id);
                        if (!is_null($outlet)) {
                            $commonDPIds = array_intersect($dpIds, $item->priceList->pluck('delivery_platform_id')->toArray());
                            $item->entityDeliveryItem = $item->entityDeliveryPlatform->whereIn('delivery_platform_id', $dpIds);
                        } else {
                            $item->entityDeliveryItem = $item->entityDeliveryPlatform;
                        }
                    } else {
                        $item->priceList = $item->prices->where('main_menu_id', $menu_id)->where('delivery_platform_id', $dp);
                        $item->entityDeliveryItem = $item->entityDeliveryPlatform->where('delivery_platform_id', $dp);
                        if (count($item->priceList) > 0) {
                            $item->availability = 1;
                        }
                        if (count($item->entityDeliveryItem) > 0) {
                            $entityItem = $item->entityDeliveryItem->first();
                            $item->availability = $entityItem->available;
                            //$item->modifiers = $entityItem->modifierList($dp);
                            $addedModList = $entityItem->modifiers->pluck('modifier_group_id')->toArray();
                            $modList = array_intersect_key($modifierGroupList, array_flip($addedModList));
                            $modList = array_values($modList);

                            $unselectedModifierIds = array_diff($addedModList, array_keys($modifierGroupList));
                            $unselectedModifiers = ModifierGroup::whereIn('id', $unselectedModifierIds)->where('status', 1)->get();
                            if (count($unselectedModifiers) > 0) {
                                foreach ($unselectedModifiers as $keyUnMod => $unslctMod) {
                                    if ($unslctMod->min_permitted > 0 && $unslctMod->min_permitted == $unslctMod->max_permitted) {
                                        $item->availability = 0;
                                    }
                                }
                            }

                            $item->modifiers = $modList;
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
                        $tmpAllergies = $item->entityDeliveryItem->first();
                        $item->allergies = (is_null($tmpAllergies)?[]:unserialize($tmpAllergies->allergies));
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
                    $item->item_category_id = null;
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
                    $categories = $item->categoriesByMainMenu($mainMenu->id);

                    if ($item->price>0 && (is_null($dp) || (count($item->entityDeliveryItem) > 0 && count($item->priceList) > 0))) {
                        if (!$active || ($active && $item->availability == 1)) {
                            if (count($categories) > 0) {
                                $item->item_category_id = $categories->first()->id;
                            }
                            $items_arr[] = $item;
                        }
                    }
                }

                return $this->success($message, $items_arr);
            } else {
                return $this->error('Could not get items');
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getCustomersByFavouriteItem($item_id)
    {
        try {
            $customers = Item::find($item_id)->customers;
            return $this->success('Successfully retrieved the customers', $customers);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getPlatformItem($itemId, $platformId, $mainMenuId)
    {
        try {
            $item = EntityDeliveryPlatform::where('entity_id', $itemId)->where('delivery_platform_id', $platformId)->get()->first();
            if (is_null($item)) {
                return $this->notFound('Item not found');
            }
            $item->prices = $item->prices->where('delivery_platform_id', $platformId)->where('main_menu_id', $mainMenuId);
            $item->has_bogo_offer = 0;
            $item->bogo_buy_quantity = 0;
            $item->bogo_get_quantity = 0;
            if (count($item->prices) > 0) {
                $item->price = ($item->prices->first()->is_sale ? $item->prices->first()->sale_price : $item->prices->first()->price);
                $item->has_bogo_offer = $item->prices->first()->has_bogo_offer;
                if ($item->has_bogo_offer && !is_null($item->prices->first()->category) && $item->prices->first()->category->is_bogo_category) {
                    $item->bogo_buy_quantity = $item->prices->first()->category->buy_quantity;
                    $item->bogo_get_quantity = $item->prices->first()->category->get_quantity;
                }
            }
            $item->modifierList = $item->modifierList($platformId);
            $item->categories = $item->item->categories;
            unset($item->item);
            unset($item->prices);
            unset($item->modifiers);
            return $this->success('Platform item.', $item);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function deleteItemImage($item_id)
    {
        try {
            $item = Item::find($item_id);
            if (is_null($item)) {
                return $this->notFound('Item not found');
            }
            $item->image_url = null;
            $item->save();
            foreach ($item->images as $key => $image) {
                $this->image_service->deleteImageFromS3($image->path);
                $image->delete();
            }
            return $this->success('Successfully deleted the item images');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }
}
