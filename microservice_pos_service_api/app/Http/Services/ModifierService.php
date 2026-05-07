<?php

namespace App\Http\Services;

use App\Http\Helpers\CommonHelper;
use App\Http\Models\EntityDeliveryPlatform;
use App\Http\Models\Item;
use App\Http\Models\Modifier;
use App\Http\Models\ModifierOption;
use App\Http\Models\ModifierGroup;
use App\Http\Models\ModifierGroupItem;
use App\Http\Models\ModifierGroupModifierItem;
use App\Http\Models\PosItem;
use App\Http\Models\ShopMainMenu;
use App\Http\Services\ItemService;
use App\Jobs\UpdateSnoozeItemList;
use App\microservice_delivergate_api\Services\BaseService as BaseService;
use App\microservice_delivergate_api\Services\RequestHandleService;
use Exception;
use Illuminate\Support\Facades\DB;

class ModifierService extends BaseService
{
    private $item_service;
    private $request_handle_service;

    public function __construct()
    {
        $this->item_service = new ItemService;
        $this->request_handle_service = new RequestHandleService;
    }

    public function index()
    {
        try {
            $modifiers = ModifierGroup::all();
            return $this->success('ModifierGroup', $modifiers->fresh('items', 'mainItems'));
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function store($data)
    {
        try {
            $message = '';
            $modifier = new ModifierGroup;
            if (isset($data['remote_id'])) {
                if (!isset($data['loyverse'])) {
                    //$modifier->id = $data['remote_id'];
                }
            }
            $modifier->remote_id = (isset($data['remote_id']) ? $data['remote_id'] : 'MOD'.time().rand(1000,9999));
            $modifier->main_menu_id = (isset($data['main_menu_id']) ? $data['main_menu_id'] : null);
            $modifier->title = $data['title'];
            $modifier->min_permitted = $data['min_selection'];
            $modifier->max_permitted = $data['max_selection'];
            $modifier->default_quantity = $data['min_selection'];
            $modifier->is_repeatable = (isset($data['is_repeatable']) ? $data['is_repeatable'] : 0);
            $modifier->is_inherited = (isset($data['is_inherited']) ? $data['is_inherited'] : 0);
            $modifier->description = (isset($data['description']) ? $data['description'] : null);
            $modifier->delivery_platform = (isset($data['delivery_platform']) ? $data['delivery_platform'] : null);
            $modifier->status = (isset($data['status']) ? $data['status'] : 1);
            $modifier->platform = (isset($data['platform']) ? $data['platform'] : (isset($data['delivery_platform']) ? $data['delivery_platform'] : null));
            DB::transaction(function () use (&$modifier, $data, &$message) {
                $modifier->save();
                if (isset($data['items'])) {
                    $this->mapModifierWithModifierItems($modifier, $data['items'], (isset($data['platform']) ? $data['platform'] : (isset($data['delivery_platform']) ? $data['delivery_platform'] : null)));
                } elseif (isset($data['item_ids']) && !isset($data['system_entry'])) {
                    $this->mapModifierWithModifierItems($modifier, $data['item_ids'], (isset($data['platform']) ? $data['platform'] : (isset($data['delivery_platform']) ? $data['delivery_platform'] : null)));
                } elseif (isset($data['item_ids']) && isset($data['system_entry'])) {
                    $this->mapModifierWithModifierItemsV2($modifier, $data['item_ids']);
                }
                if (isset($data['loyverse'])) {
                    if (isset($data['loyverse'])) {
                        $syncModifierOptions = [];
                        foreach ($data['modifier_options'] as $key => $modifier_option) {
                            $tmpPrice = $modifier_option->price;
                            $systemItem = Item::where('title', $modifier_option->name)->where('price', $tmpPrice)->get()->first();

                            if (is_null($systemItem)) {
                                $item_id = $this->item_service->store(['remote_id' => $modifier_option->id, 'title' => $modifier_option->name, 'price' => $tmpPrice, 'contains_alcohol' => 0, 'status' => 1]);
                            }
                            if (is_null($systemItem)) {
                                $systemItem = Item::where('title', $modifier_option->name)->where('price', $tmpPrice)->get()->first();
                            }

                            $response = $this->item_service->storeOrUpdatePosItem(['pos_id' => $data['pos_id'], 'shop_id' => $data['shop_id'], 'title' => $modifier_option->name, 'item_id_id' => $systemItem->id, 'pos_item_id' => $modifier_option->id, 'handle' => null, 'reference_id' => null, 'track_stock' => null, 'sold_by_weight' => null, 'is_composite' => null, 'use_production' => null, 'form' => null, 'color' => null, 'available_for_sale' => null, 'variant_id' => null, 'store_id' => null, 'cost' => 0, 'reference_variant_id' => null, 'barcode' => null, 'purchase_cost' => 0, 'default_pricing_type' => null, 'default_price' => $tmpPrice]);
                            $syncModifierOptions[] = ['id' => $systemItem->id, 'price' => $tmpPrice, 'name' => $systemItem->title];
                            /*if (!isset($data['update_modifiers']) || (isset($data['update_modifiers']) && $data['update_modifiers'])) {
                                $this->mapModifierWithModifierItemsLoyverse($modifier, [['id' => $systemItem->id, 'price' => $tmpPrice, 'name' => $systemItem->title]]);
                            }*/
                        }
                        if (!isset($data['update_modifiers']) || (isset($data['update_modifiers']) && $data['update_modifiers'])) {
                            $this->mapModifierWithModifierItemsLoyverse($modifier, $syncModifierOptions);
                        }
                    }
                }
                if (isset($data['system_entry'])) {
                    try {
                        $input = ['name' => $data['title'], 'modifier_options' => $data['item_ids'], 'modifier_id' => $modifier->id];
                        $posService = new PosService;
                        $response = $posService->createRemoteModifier($input);
                        if ($response->getStatusCode()==500) {
                            $message = ' But unable to create the modifiers in Loyverse.';
                        }
                        \Log::error($response->getContent());
                    } catch (Exception $e) {
                        $this->loggerError($e, $this, __FUNCTION__, __LINE__);
                    }
                }
            });
            return $this->success('Successfully created the ModifierGroup. '.$message, ['id' => $modifier->id]);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function mapModifierWithModifierItemsV2($modifier, $items)
    {
        try {
            $itemIds = [];
            foreach ($items as $key => $item) {
                foreach ($item['delivery_platforms'] as $key => $dp) {
                    $dps = DB::table('delivery_platform')->where('id', $dp)->orWhere('parent_platform', $dp)->where('status', 'Active')->get();
                    foreach ($dps as $key2 => $dpObj) {
                        $dp = $dpObj->id;
                        $entityItem = EntityDeliveryPlatform::where('entity_id', $item['id'])->where('delivery_platform_id', $dp)->get();
                        if (count($entityItem)>0) {
                            $entityItem = $entityItem->first();
                        } else {
                            $itemId = 'MOD-' . str_pad($dp, 3, '0', STR_PAD_LEFT) . '-' . str_pad($item['id'], 4, '0', STR_PAD_LEFT);
                            $itemObj = Item::find($item['id']);
                            $entityItem = new EntityDeliveryPlatform;
                            $entityItem->entity_id = $item['id'];
                            $entityItem->external_item_id = $itemId;
                            $entityItem->plu = $itemId;
                            $entityItem->delivery_platform_id = $dp;
                            $entityItem->price = $item['price'];
                            $entityItem->item_name = $itemObj->title;
                            $entityItem->allergies = serialize([]);
                            $entityItem->save();
                        }

                        if (!is_null($entityItem)) {
                            $modItm = ModifierGroupModifierItem::firstOrNew([
                                'modifier_group_id' => $modifier->id,
                                'item_id' => $entityItem->external_item_id,
                                'platform' => $entityItem->delivery_platform_id
                            ]);
                            $modItm->price = $item['price']*100;
                            $modItm->save();
                            $itemIds[] = $modItm->id;
                        }
                    }
                }
            }
            ModifierGroupModifierItem::where('modifier_group_id', $modifier->id)->whereNotIn('id', $itemIds)->where('platform', '!=', 'LOYVERSE')->delete();
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
        }
    }

    public function mapModifierWithModifierItems($modifier, $items, $platform = null)
    {
        try {
            $itemIds = [];
            //$priceMap = [];
            foreach ($items as $key => $item) {
                $modItm = ModifierGroupModifierItem::firstOrNew([
                    'modifier_group_id' => $modifier->id,
                    'item_id' => (isset($item['id']) ? $item['id'] : $item),
                    'platform' => $platform
                ]);
                if (isset($item['price'])) {
                    $modItm->price = (isset($item['price']) ? ((int) (((float) $item['price']) * 100)) : 0);
                }
                $modItm->save();
                $itemIds[] = $modItm->id;
                //$priceMap[$modItm->item->entity_id] = $modItm->price;
            }
            /*$modifierItemIds = EntityDeliveryPlatform::whereIn('external_item_id', $itemIds)->pluck('entity_id')->toArray();
            $additionalEntityElements = EntityDeliveryPlatform::whereIn('entity_id', $modifierItemIds)->get();
            $modifierGroupAdditionalItems = ModifierGroupModifierItem::where('modifier_group_id', $modifier->id)->whereIn('item_id', $additionalEntityElements->pluck('external_item_id')->toArray())->get();
            foreach ($modifierGroupAdditionalItems as $exKey => $modiferItem) {
                if (!in_array($modiferItem->item_id, $itemIds)) {
                    if (isset($priceMap[$modiferItem->item->entity_id])) {
                        $modiferItem->price = $priceMap[$modiferItem->item->entity_id];
                        $modiferItem->save();
                    }
                    $itemIds[] = $modiferItem->id;
                }
            }*/
            /*foreach ($additionalEntityElements as $exKey => $entityItem) {
                if (!in_array($entityItem->external_item_id, $itemIds)) {
                    $modItm = ModifierGroupModifierItem::firstOrNew([
                        'modifier_group_id' => $modifier->id,
                        'item_id' => $entityItem->external_item_id,
                    ]);
                    if (is_null($modItm->price)) {
                        $modItm->price = $entityItem->price*100;
                    }
                    $modItm->save();
                    $itemIds[] = $entityItem->external_item_id;
                }
            }*/
            ModifierGroupModifierItem::where('modifier_group_id', $modifier->id)->whereNotIn('id', $itemIds)->where('platform', $platform)->delete();
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
        }
    }

    public function mapModifierWithModifierItemsLoyverse($modifier, $items, $excludeItemIds = [])
    {
        try {
            $dpIds = EntityDeliveryPlatform::whereNotNull('delivery_platform_id')->get()->pluck('delivery_platform_id')->toArray();
            $dpIds = array_unique($dpIds);
            $itemIds = [];
            foreach ($items as $key => $item) {
                foreach ($dpIds as $key1 => $dpId) {
                    $itemId = $item['id'];
                    $entityItem = EntityDeliveryPlatform::where('entity_id', $item['id'])->where('delivery_platform_id', $dpId)->get();
                    if (count($entityItem) > 0) {
                        $itemId = $entityItem->first()->external_item_id;
                        $entityItem = $entityItem->first();
                        if (isset($item['name'])) {
                            $entityItem->price = $item['price'];
                            $entityItem->item_name = $item['name'];
                            $entityItem->save();
                        }
                    } elseif (!is_null($dpId)) {
                        $itemId = 'LM-' . str_pad($dpId, 3, '0', STR_PAD_LEFT) . '-' . str_pad($itemId, 4, '0', STR_PAD_LEFT);
                        $entityItem = new EntityDeliveryPlatform;
                        $entityItem->entity_id = $item['id'];
                        $entityItem->delivery_platform_id = $dpId;
                        $entityItem->external_item_id = $itemId;
                        $entityItem->plu = $itemId;
                        $entityItem->price = (float) $item['price'];
                        $entityItem->item_name = (isset($item['name']) ? $item['name'] : '');
                        $entityItem->allergies = serialize([]);
                        $entityItem->save();
                    }
                    $itemIds[] = $itemId;
                    $modItm = ModifierGroupModifierItem::firstOrNew([
                        'modifier_group_id' => $modifier->id,
                        'item_id' => $itemId,
                    ]);
                    $modItm->price = (int) (((float) $item['price']) * 100);
                    $modItm->platform = $dpId;
                    $modItm->save();
                }
            }
            $itemIds = array_merge($excludeItemIds, $itemIds);
            ModifierGroupModifierItem::where('modifier_group_id', $modifier->id)->whereNotIn('item_id', $itemIds)->delete();
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
        }
    }

    public function show($id)
    {
        try {
            $modifier = ModifierGroup::find($id);
            if (is_null($modifier)) {
                return $this->notFound('ModifierGroup not found');
            }
            return $this->success('ModifierGroup', $modifier->fresh('items', 'mainItems'));
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function update($data, $id)
    {
        try {
            $modifier = ModifierGroup::find($id);
            /*if (isset($data['loyverse'])) {
            $modifier = ModifierGroup::where('remote_id', $id)->first();
            } else {
            $modifier = ModifierGroup::find($id);
            }*/
            if (is_null($modifier->platform) && isset($data['remote_id'])) {
                $modifier->platform = 'LOYVERSE';
                $modifier->remote_id = $data['remote_id'];
            }

            $modifier->title = $data['title'];
            $modifier->min_permitted = (isset($data['min_selection']) ? $data['min_selection'] : $modifier->min_permitted);
            $modifier->max_permitted = (isset($data['max_selection']) ? $data['max_selection'] : $modifier->max_permitted);
            $modifier->default_quantity = (isset($data['min_selection']) ? $data['min_selection'] : $modifier->default_quantity);
            $modifier->is_repeatable = (isset($data['is_repeatable']) ? $data['is_repeatable'] : $modifier->is_repeatable);
            $modifier->is_inherited = (isset($data['is_inherited']) ? $data['is_inherited'] : $modifier->is_inherited);
            $modifier->description = (isset($data['description']) ? $data['description'] : $modifier->description);
            $modifier->delivery_platform = (isset($data['delivery_platform']) ? $data['delivery_platform'] : $modifier->delivery_platform);
            $modifier->status = (isset($data['status']) ? $data['status'] : $modifier->status);
            DB::transaction(function () use (&$modifier, $data) {
                $modifier->save();
                if (isset($data['items'])) {
                    $this->mapModifierWithModifierItems($modifier, $data['items'], (isset($data['delivery_platform']) ? $data['delivery_platform'] : $modifier->delivery_platform));
                } elseif (isset($data['item_ids']) && !isset($data['system_entry'])) {
                    $this->mapModifierWithModifierItems($modifier, $data['item_ids'], (isset($data['delivery_platform']) ? $data['delivery_platform'] : $modifier->delivery_platform));
                } elseif (isset($data['item_ids']) && isset($data['system_entry'])) {
                    $this->mapModifierWithModifierItemsV2($modifier, $data['item_ids']);
                }
                if (isset($data['loyverse'])) {
                    $items = ModifierGroupModifierItem::where('modifier_group_id', $modifier->id)->where('platform', 'LOYVERSE')->get();
                    $check_if_exists = false;
                    foreach ($items as $key => $value) {
                        $check_if_exists = false;
                        foreach ($data['modifier_options'] as $key => $modifier_option) {
                            $tmpPrice = $modifier_option->price;
                            $systemItem = null;
                            $posItem = PosItem::where('pos_item_id', $modifier_option->id)->whereNotNull('item_id_id')->get();
                            if (count($posItem) > 0) {
                                $systemItem = Item::find($posItem->first()->item_id_id);
                            }
                            if (is_null($systemItem)) {
                                $systemItem = Item::where('title', $modifier_option->name)->get()->first();
                            }
                            if (!is_null($systemItem) || (!is_null($value->item) && ($value->item->item_name == $modifier_option->name))) {
                                $item_id = $this->item_service->update(['remote_id' => $modifier_option->id, 'title' => $modifier_option->name, 'price' => $tmpPrice, 'contains_alcohol' => 0, 'status' => 1, 'loyverse' => 'true'], $modifier_option->name);
                                $response = $this->item_service->storeOrUpdatePosItem(['pos_id' => $data['pos_id'], 'shop_id' => $data['shop_id'], 'title' => $modifier_option->name, 'item_id_id' => $systemItem->id, 'pos_item_id' => $modifier_option->id, 'handle' => null, 'reference_id' => null, 'track_stock' => null, 'sold_by_weight' => null, 'is_composite' => null, 'use_production' => null, 'form' => null, 'color' => null, 'available_for_sale' => null, 'variant_id' => null, 'store_id' => null, 'cost' => 0, 'reference_variant_id' => null, 'barcode' => null, 'purchase_cost' => 0, 'default_pricing_type' => null, 'default_price' => $tmpPrice]);
                                $check_if_exists = true;
                            }
                        }
                        if ($check_if_exists == false) {
                            PosItem::whereIn('item_id_id', [$value->item->entity_id])->delete();
                            $external_ids = EntityDeliveryPlatform::where('entity_id', $value->item->entity_id)->get()->pluck('external_item_id')->toArray();
                            EntityDeliveryPlatform::where('entity_id', $value->item->entity_id)->delete();
                            ModifierGroupModifierItem::whereIn('item_id', $external_ids)->delete();
                            /*Item::whereIn('id', [$value->itemPrimary->id])->delete();
                         */
                        }
                    }
                    $items = ModifierGroupModifierItem::where('modifier_group_id', $modifier->id)->get();
                    $syncModifierOptions = [];
                    $availableModifierOptions = [];
                    foreach ($data['modifier_options'] as $key => $modifier_option) {
                        $tmpPrice = $modifier_option->price;
                        $systemItem = Item::where('title', $modifier_option->name)->where('price', $tmpPrice)->get()->first();
                        $check_if_exists = false;
                        foreach ($items as $key => $value) {
                            if ($modifier_option->name == $value->item->item_name && $modifier_option->price == $value->item->price) {
                                $check_if_exists = true;
                                $availableModifierOptions[] = $value->item->external_item_id;
                            }
                        }
                        if ($check_if_exists == false) {
                            if (count(Item::where('remote_id', $modifier_option->id)->get())==0) {
                                $item_id = $this->item_service->store(['remote_id' => $modifier_option->id, 'title' => $modifier_option->name, 'price' => $modifier_option->price, 'contains_alcohol' => 0, 'status' => 1, 'loyverse' => 'true']);
                            }
                            if (is_null($systemItem)) {
                                $posItem = PosItem::where('pos_item_id', $modifier_option->id)->get()->first();
                                if (!is_null($posItem) && !is_null($posItem->item_id_id)) {
                                    Item::where('id', $posItem->item_id_id)->update(['title' => $modifier_option->name, 'price' => $tmpPrice]);
                                }
                                $systemItem = Item::where('title', $modifier_option->name)->where('price', $tmpPrice)->get()->first();
                            }
                            $response = $this->item_service->storeOrUpdatePosItem(['pos_id' => $data['pos_id'], 'shop_id' => $data['shop_id'], 'title' => $modifier_option->name, 'item_id_id' => $systemItem->id, 'pos_item_id' => $modifier_option->id, 'handle' => null, 'reference_id' => null, 'track_stock' => null, 'sold_by_weight' => null, 'is_composite' => null, 'use_production' => null, 'form' => null, 'color' => null, 'available_for_sale' => null, 'variant_id' => null, 'store_id' => null, 'cost' => 0, 'reference_variant_id' => null, 'barcode' => null, 'purchase_cost' => 0, 'default_pricing_type' => null, 'default_price' => $tmpPrice]);
                            $syncModifierOptions[] = ['id' => $systemItem->id, 'price' => $modifier_option->price, 'name' => $modifier_option->name];
                            // if (!isset($data['update_modifiers']) || (isset($data['update_modifiers']) && $data['update_modifiers'])) {
                            //     $this->mapModifierWithModifierItemsLoyverse($modifier, [['id' => $systemItem->id, 'price' => $modifier_option->price, 'name' => $modifier_option->name]]);
                            // }
                        }
                    }
                    if (!isset($data['update_modifiers']) || (isset($data['update_modifiers']) && $data['update_modifiers'])) {
                        $this->mapModifierWithModifierItemsLoyverse($modifier, $syncModifierOptions, $availableModifierOptions);
                    }
                }

                if (isset($data['system_entry'])) {
                    try {
                        $input = ['name' => $data['title'], 'modifier_options' => $data['item_ids'], 'modifier_id' => $modifier->id, 'type' => 'UPDATE'];
                        $posService = new PosService;
                        $response = $posService->createRemoteModifier($input);
                        if ($response->getStatusCode()==500) {
                            $message = ' But unable to create the modifiers in Loyverse.';
                        }
                    } catch (Exception $e) {
                        $this->loggerError($e, $this, __FUNCTION__, __LINE__);
                    }
                }
            });
            $shopIds = [];
            if (isset($data['main_menu_id']) && $data['main_menu_id'] != '') {
                $shopIds = ShopMainMenu::where('main_menu_id', $data['main_menu_id'])->pluck('shop_id')->toArray();
            } else {
                $shopIds = ShopMainMenu::where('main_menu_id', $modifier->main_menu_id)->pluck('shop_id')->toArray();
            }
            // UpdateSnoozeItemList::dispatch(['shopIds' => $shopIds, 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
            return $this->success('Successfully updated the ModifierGroup.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function modifierGroupItems($id)
    {
        try {
            $modifier = ModifierGroup::find($id);
            if (is_null($modifier)) {
                return $this->notFound('ModifierGroup not found');
            }
            return $this->success('ModifierGroup items', $modifier->mainItems->fresh('item', 'modifier'));
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function modifierGroupModifierItems($id)
    {
        try {
            $modifier = ModifierGroup::find($id);
            if (is_null($modifier)) {
                return $this->notFound('ModifierGroup not found');
            }
            return $this->success('ModifierGroup items', $modifier->items->fresh('item', 'modifier'));
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function destroy($id)
    {
        try {
            $modifier = ModifierGroup::find($id);
            if (is_null($modifier)) {
                return $this->notFound('ModifierGroup not found');
            }
            DB::transaction(function () use (&$modifier, $id) {
                ModifierGroupItem::where('modifier_group_id', $id)->delete();
                ModifierGroupModifierItem::where('modifier_group_id', $id)->delete();
                $modifier->delete();
            });
            $shopIds = ShopMainMenu::where('main_menu_id', $modifier->main_menu_id)->pluck('shop_id')->toArray();
            // UpdateSnoozeItemList::dispatch(['shopIds' => $shopIds, 'tenantCode' => CommonHelper::getXTenantCode($_SERVER)])->onConnection('sqs3');
            return $this->success('Successfully deleted the ModifierGroup');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function syncPosModifiers($platformId)
    {
        try {
            $platformResponse = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform/' . $platformId, null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platform = (json_decode($platformResponse->getBody()))->data;
            $currentPos = CommonHelper::getCurrentPos($platform->outlet_id);
            if (!is_null($currentPos) && $currentPos->name == 'LOYVERSE') {
                $itemIds = [];
                $modifierGroupItem = ModifierGroupItem::where('platform', 'LOYVERSE')->get();
                $modifierGroupModifierItem = ModifierGroupModifierItem::where('platform', 'LOYVERSE')->get();

                foreach ($modifierGroupItem as $key => $item) {
                    $entityItem = EntityDeliveryPlatform::where('entity_id', $item->item_id)->where('delivery_platform_id', $platformId)->get()->first();
                    if (is_null($entityItem)) {
                        $mainItem = Item::find($item->item_id);
                        if (!is_null($mainItem)) {
                            $newEntityItem = new EntityDeliveryPlatform;
                            $newEntityItem->entity_id = $item->item_id;
                            $newEntityItem->delivery_platform_id = $platformId;
                            $newEntityItem->external_item_id = 'MOD' . $item->item_id;
                            $newEntityItem->plu = 'MOD' . $item->item_id;
                            $newEntityItem->price = $mainItem->price;
                            $newEntityItem->item_name = $mainItem->title;
                            $newEntityItem->allergies = serialize([]);
                            $newEntityItem->save();

                            $modItem = ModifierGroupItem::where('item_id', $newEntityItem->external_item_id)->where('modifier_group_id', $item->modifier_group_id)->get();
                            if (count($modItem) == 0) {
                                $modItem = new ModifierGroupItem;
                                $modItem->modifier_group_id = $item->modifier_group_id;
                                $modItem->item_id = $newEntityItem->external_item_id;
                                $modItem->save();
                            }
                        }
                    } else {
                        $modItem = ModifierGroupItem::where('item_id', $entityItem->external_item_id)->where('modifier_group_id', $item->modifier_group_id)->get();
                        if (count($modItem) == 0) {
                            $modItem = new ModifierGroupItem;
                            $modItem->modifier_group_id = $item->modifier_group_id;
                            $modItem->item_id = $entityItem->external_item_id;
                            $modItem->save();
                        }
                    }
                }

                foreach ($modifierGroupModifierItem as $key => $item) {
                    $entityItem = EntityDeliveryPlatform::where('entity_id', $item->item_id)->where('delivery_platform_id', $platformId)->get()->first();
                    if (is_null($entityItem)) {
                        $mainItem = Item::find($item->item_id);
                        if (!is_null($mainItem)) {
                            $newEntityItem = new EntityDeliveryPlatform;
                            $newEntityItem->entity_id = $item->item_id;
                            $newEntityItem->delivery_platform_id = $platformId;
                            $newEntityItem->external_item_id = 'MOD' . $item->item_id;
                            $newEntityItem->plu = 'MOD' . $item->item_id;
                            $newEntityItem->price = $mainItem->price;
                            $newEntityItem->item_name = $mainItem->title;
                            $newEntityItem->allergies = serialize([]);
                            $newEntityItem->save();

                            $modItem = ModifierGroupModifierItem::where('item_id', $newEntityItem->external_item_id)->where('modifier_group_id', $item->modifier_group_id)->get();
                            if (count($modItem) == 0) {
                                $modItem = new ModifierGroupModifierItem;
                                $modItem->modifier_group_id = $item->modifier_group_id;
                                $modItem->item_id = $newEntityItem->external_item_id;
                                $modItem->save();
                            }
                        }
                    } else {
                        $modItem = ModifierGroupModifierItem::where('item_id', $entityItem->external_item_id)->where('modifier_group_id', $item->modifier_group_id)->get();
                        if (count($modItem) == 0) {
                            $modItem = new ModifierGroupModifierItem;
                            $modItem->modifier_group_id = $item->modifier_group_id;
                            $modItem->item_id = $entityItem->external_item_id;
                            $modItem->price = $entityItem->price;
                            $modItem->save();
                        }
                    }
                }
                return $this->success('Successfully Fetched the items');
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function deleteModifiers($id)
    {
        $item_ids = ModifierGroupModifierItem::where('modifier_group_id', $id)->get()->pluck('item_id')->toArray();
        Item::whereIn('id', $item_ids)->delete();
        ModifierGroupModifierItem::where('modifier_group_id', $id)->delete();
        ModifierGroupItem::where('modifier_group_id', $id)->delete();
        EntityDeliveryPlatform::whereIn('entity_id', $item_ids)->delete();
        ModifierGroup::whereIn('id', [$id])->delete();
    }

    public function storeOrUpdatePosModifiers($data)
    {
        try {
            DB::transaction(function () use ($data) {
                $modifier = Modifier::firstOrNew([
                    'source_type_id' => $data['source_type_id'],
                    'source_type' => $data['platform_type'],
                    'shop_id' => $data['shop_id'],
                    'remote_id' => $data['remote_id'],
                ]);
                if (isset($data['modifier_group_id']) && is_null($modifier->platform)) {
                    $modifier->modifier_group_id = $data['modifier_group_id'];
                }
                $modifier->title = $data['title'];
                $modifier->sub_title = (isset($data['sub_title']) ? $data['sub_title'] : null);
                $modifier->description = (isset($data['description']) ? $data['description'] : null);
                if (!is_null($data['min_selection'])) {
                    $modifier->min_permitted = $data['min_selection'];
                    $modifier->default_quantity = $data['min_selection'];
                }
                if (!is_null($data['max_selection'])) {
                    $modifier->max_permitted = $data['max_selection'];
                }
                $modifier->status = (isset($data['status']) ? $data['status'] : 1);
                $modifier->save();

                if (is_null($modifier->modifier_group_id)) {
                    $hasModifier = false;
                    $modGroupId = null;
                    $modGroups = ModifierGroup::where('title', $modifier->title)->where('platform', 'LOYVERSE')->get();
                    $dpIds = EntityDeliveryPlatform::whereNotNull('delivery_platform_id')->get()->pluck('delivery_platform_id')->toArray();
                    $dpIds = array_unique($dpIds);
                    foreach ($modGroups as $key => $modGroup) {
                        if (!$hasModifier && (count($modGroup->items)/count($dpIds))==count($data['modifier_options'])) {
                            $matchedOptions = 0;
                            $checkedOptions = [];
                            foreach ($modGroup->items->pluck('item_id')->toArray() as $key1 => $modOpt) {
                                $exploded = explode('-', $modOpt);
                                if (isset($exploded[2]) && !in_array($exploded[2], $checkedOptions)) {
                                    $item = Item::find((int)$exploded[2]);
                                    if (!is_null($item) && count(collect($data['modifier_options'])->where('name', $item->title))>0) {
                                        $matchedOptions++;
                                    }
                                    $checkedOptions[] = $exploded[2];
                                }
                            }
                            if ($matchedOptions==count($data['modifier_options'])) {
                                $modGroupId = $modGroup->id;
                                /*$modifier->modifier_group_id = $modGroup->id;
                                $modifier->save();*/
                                $hasModifier = true;
                            }
                        }
                    }
                    if (!$hasModifier) {
                        $response = $this->store($data);
                        $response = (json_decode($response->getContent()))->data;
                        $modGroupId = $response->id;
                    }
                    if (!is_null($modGroupId)) {
                        $modifier->modifier_group_id = $modGroupId;
                        $modifier->save();
                    }
                } else {
                    $this->update($data, $modifier->modifier_group_id);
                }

                foreach ($data['modifier_options'] as $key => $option) {
                    $modifierOption = ModifierOption::firstOrNew([
                        'modifier_id' => $modifier->id,
                        'remote_id' => $option->id,
                        'name' => $option->name,
                    ]);
                    $modifierOption->price      = $option->price*100;
                    $modifierOption->position   = $option->position;
                    $modifierOption->save();
                }
            });
            return $this->success('Successfully updated the modifier');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function findModifierFromItemAndModifierItem($data)
    {
        try {
            $modifierGroupItem = ModifierGroupItem::where('item_id', $data['item_id'])->pluck('modifier_group_id')->toArray();
            $modifierGroupModifierItem = ModifierGroupModifierItem::where('platform', $data['platform_id'])->where('item_id', $data['option_id'])->pluck('modifier_group_id')->toArray();
            $commonModifierIds = array_intersect($modifierGroupItem, $modifierGroupModifierItem);
            $modifier = ModifierGroup::whereIn('id', $commonModifierIds)->get()->first();
            return $this->success('Modifier', $modifier);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }
}
