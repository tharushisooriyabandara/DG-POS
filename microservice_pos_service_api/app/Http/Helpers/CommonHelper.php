<?php
namespace App\Http\Helpers;

use Auth;
use Exception;
use App\Http\Models\Configuration;
use App\Http\Models\ModifierGroup;
use App\Http\Models\ModifierGroupModifierItem;
use App\Http\Models\Pos;
use App\microservice_delivergate_api\Models\Shop;
use Illuminate\Support\Facades\Log;
use App\microservice_delivergate_api\Models\ActivityLog;

class CommonHelper
{
    public static function slugify($text)
    {
        // replace non letter or digits by -
        $text = preg_replace('~[^\pL\d]+~u', '-', $text);

        // transliterate
        $text = iconv('utf-8', 'us-ascii//TRANSLIT', $text);

        // remove unwanted characters
        $text = preg_replace('~[^-\w]+~', '', $text);

        // trim
        $text = trim($text, '-');

        // remove duplicate -
        $text = preg_replace('~-+~', '-', $text);

        // lowercase
        $text = strtolower($text);

        if (empty($text)) {
            return 'n-a';
        }

        return $text;
    }

    public static function getConfiguration($key, $shop = null)
    {
        $conf = Configuration::where('shop_id', $shop)->where('key', $key)->get()->first();
        return $conf;
    }

    public static function getConfigurationValue($key, $shop = null)
    {
        $conf = CommonHelper::getConfiguration($key, $shop);
        if (is_null($conf)) {
            return null;
        }
        return $conf->value;
    }

    public static function getShopCodeById($id)
    {
        $shop = Shop::find($id);
        return (is_null($shop) ? null : $shop->code);
    }

    public static function getXTenantCode($data)
    {
        if (isset($data['HTTP_X_TENANT_CODE'])) {
            return $data['HTTP_X_TENANT_CODE'];
        } else {
            $tenant = str_replace('.' . $data['SITE_PATH'], '', $data['HTTP_HOST']);
            return $tenant;
        }
    }

    public static function getMasterXTenantCode($data)
    {
        if (isset($data['HTTP_MASTER_X_TENANT_CODE'])) {
            return $data['HTTP_MASTER_X_TENANT_CODE'];
        } else {
            Log::error("No master x tenant code");
        }
    }

    public static function getOrderSyncStatus($data)
    {
        if (isset($data['HTTP_SYNC_STATUS'])) {
            return $data['HTTP_SYNC_STATUS'];
        } else {
            return false;
        }
    }

    public static function isModifier($id)
    {
        return (count(ModifierGroupModifierItem::where('item_id', $id)->get()) > 0);
    }

    public static function getCurrentPos($shop_id = null)
    {
        if (is_null($shop_id)) {
            $pos = Pos::where('status', 1)->get()->first();
        } else {
            $pos = Pos::where('shop_id', $shop_id)->where('status', 1)->get()->first();
        }
        if (!is_null($pos)) {
            return $pos;
        }
        return null;
    }

    public static function structureModifiers($entityItem, $dp, $mainMenuId)
    {
        $modifierOptions = [];
        $addedModifiers = [];
        foreach ($entityItem->modifiers as $ekey => $entityModifier) {
            if (!is_null($entityModifier->modifier) && $entityModifier->modifier->main_menu_id==$mainMenuId) {
                foreach ($entityModifier->modifier->items->where('platform', $dp) as $key => $mod) {
                    if (!is_null($mod->item)/* && count($mod->item->prices->where('delivery_platform_id', $dp))>0*/) {
                        $realItem = $mod->item->item;
                        if (!isset($modifierOptions[$mod->modifier_group_id]['modifier'])) {
                            unset($mod->modifier->created_at);
                            unset($mod->modifier->updated_at);
                            $modifierOptions[$mod->modifier_group_id]['modifier'] = $mod->modifier;
                        }
                        if (!in_array($mod->item_id, $addedModifiers)) {
                            $modItem = $mod->entityItem;
                            if (!is_null($modItem) && $modItem->available) {
                                $modItemList = CommonHelper::structureModifiers($mod->entityItem, $dp, $mainMenuId)/* $mod->entityItem->modifierList($dp)*/;

                                $modItem->price = number_format(($mod->price==0?0:$mod->price/100), 2, '.', '');

                                $modItemListNew = [];
                                foreach ($modItemList as $key1 => $modItemEl) {
                                    $itemArray = [];
                                    if (isset($modItemEl["items"])) {
                                        foreach ($modItemEl["items"] as $key2 => $modItmSingle) {
                                            $modGrpModItm = $modItmSingle->modifierGroupModifierItems->where('platform', $dp)->where('modifier_group_id', $modItemEl['modifier']->id);
                                            if (count($modGrpModItm)>0) {
                                                $modItmSingle->price = number_format(($modGrpModItm->first()->price==0?0:$modGrpModItm->first()->price/100), 2, '.', '');
                                            }
                                            unset($modItmSingle->modifierGroupModifierItems);
                                            $itemArray[] = $modItmSingle;
                                        }
                                        $modItemEl['items'] = $itemArray;
                                        $modItemListNew[] = $modItemEl;
                                    }
                                }
                                $modItem->tax_profile_id = $realItem->tax_profile_id;
                                $modItem->printer_groups = $realItem->printerGroups->map(function ($printerGroup) {
                                    unset($printerGroup->brand_id);
                                    unset($printerGroup->shop_id);
                                    unset($printerGroup->created_at);
                                    unset($printerGroup->updated_at);
                                    unset($printerGroup->pivot);
                                    return $printerGroup;
                                });
                                $modItem->modifier_list = $modItemListNew;
                                unset($modItem->modifiers);
                                unset($modItem->created_at);
                                unset($modItem->updated_at);
                                unset($modItem->item);
                                $modifierOptions[$mod->modifier_group_id]['items'][] = $modItem;
                                $addedModifiers[] = $mod->item_id;
                            }
                        }
                    }
                }
            }
        }
        return array_values($modifierOptions);
    }

    public static function getModifierGroups($dp, $mainMenuId)
    {
        $modifierList = [];
        $modifierGroups = ModifierGroup::where('status', 1)->where('main_menu_id', $mainMenuId)->get();
        foreach ($modifierGroups as $key => $modifierGroup) {
            $modifierItems = [];
            foreach ($modifierGroup->items->where('platform', $dp) as $key => $item) {
                $entityItem = $item->item;
                if (is_null($entityItem)) {
                    \Log::error((array)$item);
                }
                $realItem = $entityItem->item;
                if ($entityItem->prices->where('delivery_platform_id', $dp) && $entityItem->available) {
                    $modifierItemList = [];
                    if (count($entityItem->modifiers)>0) {
                        $modifierItemList = CommonHelper::structureModifiers($entityItem, $dp, $mainMenuId);
                    }
                    $entityItem->price = number_format(($item->price==0?0:$item->price/100), 2, '.', '');
                    $entityItem->tax_profile_id = $realItem->tax_profile_id;
                    $entityItem->printer_groups = $realItem->printerGroups->map(function ($printerGroup) {
                        unset($printerGroup->brand_id);
                        unset($printerGroup->shop_id);
                        unset($printerGroup->created_at);
                        unset($printerGroup->updated_at);
                        unset($printerGroup->pivot);
                        return $printerGroup;
                    });
                    $entityItem->modifier_list = $modifierItemList;
                    unset($entityItem->created_at);
                    unset($entityItem->updated_at);
                    unset($entityItem->prices);
                    unset($entityItem->modifiers);
                    unset($entityItem->item);
                    $modifierItems[] = $entityItem;
                }
            }
            unset($modifierGroup->created_at);
            unset($modifierGroup->updated_at);
            unset($modifierGroup->items);
            if (count($modifierItems)>0) {
                $modifierList[$modifierGroup->id]['modifier'] = $modifierGroup;
                $modifierList[$modifierGroup->id]['items'] = $modifierItems;
            }
        }
        return $modifierList;

    }

    public static function generateRandomCode($length)
    {
        $characters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890';
        $charactersLength = strlen($characters);
        $randomString = '';
        for ($i = 0; $i < $length; $i++) {
            $randomString .= $characters[rand(0, $charactersLength - 1)];
        }
        return  $randomString;
    }

    public static function userLog($user = null, $data)
    {
        if (is_null($user)) {
            $user = Auth::guard('api')->user();
        }
        if (!is_null($user)) {
            $log = new ActivityLog();
            $log->webshop_brand_id = isset($data['webshop_brand_id']) ? $data['webshop_brand_id'] : null;
            $log->webshop_brand_name = isset($data['webshop_brand_name']) ? $data['webshop_brand_name'] : null;
            $log->shop_id = isset($data['shop_id']) ? $data['shop_id'] : null;
            $log->causer_id = $user->id;
            $log->causer_first_name = $user->name;
            $log->causer_last_name = $user->last_name;
            $log->causer_email = $user->email;
            $log->causer_country_code = $user->country_code;
            $log->causer_contact_no = $user->contact_no;
            $log->description = $data['description'];
            $log->event = $data['event'];
            $log->subject_type = $data['subject_type'];
            $log->subject_id = $data['subject_id'];
            $log->application = isset($data['application']) ? $data['application'] : null;
            $log->save();
        }
    }
}
