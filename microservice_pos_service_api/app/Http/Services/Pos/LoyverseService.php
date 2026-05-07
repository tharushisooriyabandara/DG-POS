<?php

namespace App\Http\Services\Pos;

use App\Http\Helpers\DateTimeUtility;
use App\Http\Models\Category;
use App\Http\Models\EntityDeliveryPlatform;
use App\Http\Models\Item;
use App\Http\Models\ItemCategory;
use App\Http\Models\Modifier;
use App\Http\Models\ModifierGroup;
use App\Http\Models\PaymentType;
use App\Http\Models\Pos;
use App\Http\Models\PosCategory;
use App\Http\Models\PosItem;
use App\Http\Models\Receipt;
use App\Http\Models\ReceiptItem;
use App\Http\Models\Tax;
use App\Http\Services\CategoryService;
use App\Http\Services\ItemService;
use App\Http\Services\ModifierService;
use App\Http\Services\PaymentService;
use App\Http\Services\TaxService;
use App\microservice_delivergate_api\Models\Customer;
use App\microservice_delivergate_api\Services\BaseService as BaseService;
use App\microservice_delivergate_api\Services\RequestHandleService;
use Exception;
use Illuminate\Support\Facades\DB;

class LoyverseService extends BaseService
{
    private $pos;
    private $headers;
    private $loyverse_auth_token;
    private $loyverse_version;
    private $item_service;
    private $payment_service;
    private $loyverse_base_url;
    private $category_service;
    private $modifier_service;
    private $tax_service;
    private $request_handle_service;

    public function __construct()
    {
        $this->item_service = new ItemService;
        $this->modifier_service = new ModifierService;
        $this->category_service = new CategoryService;
        $this->tax_service = new TaxService;
        $this->payment_service = new PaymentService;
        $this->request_handle_service = new RequestHandleService;
        /*$this->loyverse_base_url = unserialize((DB::table('pos')->where('id', 2)->get())->first()->parameter_values)['LOYVERSE_BASE_URL'];
    $this->loyverse_base_url = unserialize((DB::table('pos')->where('id', 2)->get())->first()->parameter_values)['LOYVERSE_BASE_URL'];
    $this->loyverse_version = unserialize((DB::table('pos')->where('id', 2)->get())->first()->parameter_values)['LOYVERSE_VERSION'];
    $this->loyverse_auth_token = unserialize((DB::table('pos')->where('id', 2)->get())->first()->parameter_values)['LOYVERSE_ACCESS_TOKEN'];
    $this->headers = [
    'Authorization' => 'Bearer ' . $this->loyverse_auth_token,
    'Content-Type' => 'application/xml',
    ];*/
    }

    public function setPosValues($shop_id)
    {
        $this->pos = Pos::where('shop_id', $shop_id)->where('name', 'LOYVERSE')->where('status', 1)->get()->first();
        if (is_null($this->pos)) {
            $this->pos = Pos::where('shop_id', $shop_id)->where('name', 'LOYVERSE')->get()->first();
        }
        if (is_null($this->pos)) {
            return 'NOT_FOUND';
        }
        $paramValues = unserialize($this->pos->parameter_values);
        $paramValues = json_decode(json_encode($paramValues));
        $this->loyverse_base_url = $paramValues->LOYVERSE_BASE_URL;
        $this->loyverse_version = $paramValues->LOYVERSE_VERSION;
        $this->loyverse_auth_token = $paramValues->LOYVERSE_ACCESS_TOKEN;
        $this->headers = [
            'Authorization' => 'Bearer ' . $this->loyverse_auth_token,
            'Content-Type' => 'application/xml',
        ];
    }

    public function getPosCategories($shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendGetRequst(null, $this->loyverse_version . '/categories?categories_ids=&limit=&cursor=&show_deleted=false', $this->loyverse_base_url, $this->headers);
            $categories = json_decode($response->getBody());
            return $this->success('Category list', $categories);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getPosCategoryById($id, $shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendGetRequst(null, $this->loyverse_version . '/categories/' . $id, $this->loyverse_base_url, $this->headers);
            $category = json_decode($response->getBody());
            return $this->success('Category', $category);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function fetchPosCategories($shop_id)
    {
        try {
            $categories = $this->getPosCategories($shop_id);
            $categories = json_decode($categories->getContent());
            DB::transaction(function () use ($categories) {
                $categoryIds = [];
                foreach ($categories->data->categories as $key => $category) {
                    $posCategory = PosCategory::where('pos_id', $this->pos->id)->where('remote_id', $category->id)->get()->first();
                    if (!is_null($posCategory) && !is_null($posCategory->category_id)) {
                        $cat  = Category::where('id', $posCategory->category_id)->update(['title' => $category->name]);
                    }
                    $systemCategory = Category::where('title', $category->name)->get()->first();
                    if (is_null($systemCategory)) {
                        $this->category_service->store(['remote_id' => $category->id, 'title' => $category->name, 'sub_title' => '', 'image_path' => null, 'parent_id' => null, 'description' => $category->color, 'status' => 1]);
                    }
                    if (is_null($systemCategory)) {
                        $systemCategory = Category::where('title', $category->name)->get()->first();
                    }
                    $categoryIds[] = $category->id;
                    $this->category_service->storeOrUpdatePosCategory(['remote_id' => $category->id, 'category_id' => $systemCategory->id, 'pos_id' => $this->pos->id, 'title' => $category->name, 'sub_title' => '', 'image_path' => null, 'parent_id' => 0, 'description' => '', 'status' => 1]);
                }
                PosCategory::where('pos_id', $this->pos->id)->whereNotIn('remote_id', $categoryIds)->delete();
            });
            return $this->success('Successfully fetched the categories');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getPosItems($shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendGetRequst(null, $this->loyverse_version . '/items?items_ids=&created_at_min=&created_at_max=&updated_at_min=&updated_at_max=&limit=250&cursor=&show_deleted=false', $this->loyverse_base_url, $this->headers);
            $items = json_decode($response->getBody());
            return $this->success('Items', $items);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getPosItemById($id, $shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendGetRequst(null, $this->loyverse_version . '/items/' . $id, $this->loyverse_base_url, $this->headers);
            $item = json_decode($response->getBody());
            return $this->success('Items', $item);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function fetchPosItems($shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $items = $this->getPosItems($shop_id);
            $items = json_decode($items->getContent());
            DB::transaction(function () use ($items) {
                $posItemIds = [];
                foreach ($items->data->items as $key => $item) {
                    foreach ($item->variants as $key_1 => $variant) {
                        if ($item->option1_name == null) {
                            $itemName = $item->item_name;
                        } else if ($item->option1_name != null && $item->option2_name != null && $item->option3_name != null) {
                            $itemName = $item->item_name . ' (' . $item->option1_name . ': ' . $variant->option1_value . '/' . $item->option2_name . ': ' . $variant->option2_value . '/' . $item->option3_name . ': ' . $variant->option3_value . ')';
                        } else if ($item->option1_name != null && $item->option2_name != null) {
                            $itemName = $item->item_name . ' (' . $item->option1_name . ': ' . $variant->option1_value . '/' . $item->option2_name . ': ' . $variant->option2_value . ')';
                        } else {
                            $itemName = $item->item_name . ' (' . $item->option1_name . ': ' . $variant->option1_value . ')';
                        }

                        $posItem = PosItem::where('pos_id', $this->pos->id)->where('variant_id', $variant->variant_id)->whereNotNull('item_id_id')->get();
                        if (count($posItem) > 0) {
                            $systemItem = Item::find($posItem->first()->item_id_id);
                        } else {
                            $systemItem = Item::where('title', $itemName)->first();
                        }
                        $tmpPrice = round($variant->default_price, 2);
                        if (isset($variant->stores[0])) {
                            $tmpPrice = round($variant->stores[0]->price, 2);
                        }
                        if (is_null($systemItem)) {
                            $response = $this->item_service->store(['pos_id' => $this->pos->id, 'source' => 'POS', 'title' => $itemName, 'description' => '', 'image_url' => $item->image_url, 'tax' => 0, 'price' => $tmpPrice, 'sku' => $variant->sku, 'status' => 1, 'category_id' => $item->category_id]);
                            $systemItem = Item::where('title', $itemName)->where('price', $tmpPrice)->get()->first();
                        } else {
                            /*
                                $systemItem->update(['price' => $tmpPrice, 'sku' => $variant->sku]);
                                We cannot do this. Since we have multiple pos with same item name, it will update this record again and again.
                            */
                            $posCategory = PosCategory::where('pos_id', $this->pos->id)->where('remote_id', $item->category_id)->get()->first();
                            if (!is_null($posCategory)) {
                                $itemCategory = ItemCategory::firstOrNew([
                                    'item_id' => $systemItem->id,
                                    'main_menu_id' => null,
                                ]);
                                $itemCategory->category_id = $posCategory->category_id;
                                $itemCategory->save();
                            }
                            /*$response = $this->item_service->update(['remote_id' => $item->id, 'title' => $itemName, 'handle' => $item->handle, 'image_url' => $item->image_url, 'tax' => 0, 'price' => $tmpPrice, 'sku' => $variant->sku, 'status' => 1, 'category_id' => $item->category_id, 'form' => $item->form, 'color' => $item->color, 'variant_id' => $variant->variant_id, 'cost' => $variant->cost, 'tax_ids' => $item->tax_ids, 'modifier_ids' => $item->modifier_ids, 'loyverse_item' => 'true', 'contains_alcohol' => 0, 'store_id' => $variant->stores[0]->store_id, 'purchase_cost' => $variant->purchase_cost, 'default_pricing_type' => $variant->default_pricing_type, 'stores' => $variant->stores], $variant->variant_id);*/
                        }
                        $posItemIds[] = $item->id;
                        $response = $this->item_service->storeOrUpdatePosItem(['pos_id' => $this->pos->id, 'shop_id' => $this->pos->shop_id, 'item_id_id' => $systemItem->id, 'title' => $itemName, 'pos_item_id' => $item->id, 'handle' => $item->handle, 'reference_id' => $item->reference_id, 'track_stock' => $item->track_stock, 'sold_by_weight' => $item->sold_by_weight, 'is_composite' => $item->is_composite, 'use_production' => $item->use_production, 'form' => $item->form, 'color' => $item->color, 'available_for_sale' => $variant->stores[0]->available_for_sale, 'variant_id' => $variant->variant_id, 'store_id' => $variant->stores[0]->store_id, 'cost' => $variant->cost, 'reference_variant_id' => $variant->reference_variant_id, 'barcode' => $variant->barcode, 'purchase_cost' => $variant->purchase_cost, 'default_pricing_type' => $variant->default_pricing_type, 'default_price' => $tmpPrice, 'stores' => $variant->stores, 'loyverse_item' => 'true', 'tax_ids' => $item->tax_ids]);
                    }
                }

                //delete Items which are deleted from POS
                //$item = PosItem::where('pos_id', $this->pos->id)->where('variant_id', '!=', null)->get();
                $item = PosItem::where('pos_id', $this->pos->id)->whereNotIn('pos_item_id', $posItemIds)->where('available_for_sale', 1)->delete();
                /*foreach ($item as $key => $value) {
                $check = false;
                foreach ($items->data->items as $key_2 => $item) {
                foreach ($item->variants as $key_1 => $variant) {
                if ($value->variant_id == $variant->variant_id) {
                $check = true;
                }
                }
                }
                if ($check == false) {
                $this->item_service->destroy($value->id, null);
                }
                }*/

                if ($response->getStatusCode() != 200) {
                    throw new exception("Internal service error");
                }
            });

            return $this->success('Successfully fetched the items');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getPosModifiers($shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendGetRequst(null, $this->loyverse_version . '/modifiers?modifier_ids=&created_at_min=&created_at_max=&updated_at_min=&updated_at_max=&show_deleted=false&limit=&cursor=', $this->loyverse_base_url, $this->headers);
            $modifiers = json_decode($response->getBody());
            return $this->success('modifiers', $modifiers);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getPosModifierById($id, $shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendGetRequst(null, $this->loyverse_version . '/modifiers/' . $id, $this->loyverse_base_url, $this->headers);
            $modifier = json_decode($response->getBody());
            return $this->success('modifier', $modifier);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function fetchPosModifiers($shop_id, $update_modifiers = true)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $modifiers = $this->getPosModifiers($shop_id);
            $modifiers = json_decode($modifiers->getContent());
            DB::transaction(function () use ($modifiers, $update_modifiers) {
                $modifierIds = [];
                foreach ($modifiers->data->modifiers as $key => $modifier) {
                    /*$systemItem = ModifierGroup::where('remote_id', $modifier->id)->first();
                    if (is_null($systemItem)) {
                        $response = $this->modifier_service->store(['pos_id' => $this->pos->id, 'shop_id' => $this->pos->shop_id,  'title' => $modifier->name, 'modifier_options' => $modifier->modifier_options, 'loyverse' => "true", 'min_selection' => null, 'max_selection' => null, 'description' => null, 'delivery_platform' => null, 'status' => 1, 'update_modifiers' => $update_modifiers, 'remote_id' => $modifier->id, 'platform' => 'LOYVERSE']);
                    } else {
                        $response = $this->modifier_service->update(['pos_id' => $this->pos->id, 'shop_id' => $this->pos->shop_id, 'title' => $modifier->name, 'modifier_options' => $modifier->modifier_options, 'loyverse' => "true", 'min_selection' => null, 'max_selection' => null, 'description' => null, 'delivery_platform' => null, 'status' => 1, 'update_modifiers' => $update_modifiers], $systemItem->id);
                    }
                    if ($response->getStatusCode() != 200) {
                        throw new exception("Internal service error");
                    }*/
                    $posResponse = $this->modifier_service->storeOrUpdatePosModifiers(['pos_id' => $this->pos->id, 'source_type_id' => $this->pos->id, 'shop_id' => $this->pos->shop_id, 'remote_id' => $modifier->id, 'title' => $modifier->name, 'modifier_options' => $modifier->modifier_options, 'loyverse' => "true", 'min_selection' => 0, 'max_selection' => 0, 'description' => null, 'delivery_platform' => null, 'status' => 1, 'platform' => 'LOYVERSE', 'update_modifiers' => $update_modifiers, 'platform_type' => 'POS']);
                    $modifierIds[] = $modifier->id;
                }
                //delete Modifiers which deleted from pos
                Modifier::whereNotIn('remote_id', $modifierIds)->where('source_type', 'POS')->where('source_type_id', $this->pos->id)->delete();

                /*$allModifiers = ModifierGroup::whereNotIn('id', $posModifiers)->where('platform', 'LOYVERSE')->get();
                foreach ($allModifiers as $key => $value) {
                    $check = false;
                    foreach ($modifiers->data->modifiers as $key => $modifier) {
                        if ($value->remote_id == $modifier->id) {
                            $check = true;
                        }
                    }
                    if ($check == false) {
                        $this->modifier_service->deleteModifiers($value->id);
                    }
                    $this->modifier_service->deleteModifiers($value->id);
                }*/
            });
            return $this->success('Successfully fetched the Modifiers');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getPosTaxes($shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendGetRequst(null, $this->loyverse_version . '/taxes?created_at_min=&created_at_max=&updated_at_min=&updated_at_max=&show_deleted=false', $this->loyverse_base_url, $this->headers);
            $taxes = json_decode($response->getBody());
            return $this->success('taxes', $taxes);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function fetchPosTaxes($shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $taxes = $this->getPosTaxes($shop_id);
            $taxes = json_decode($taxes->getContent());
            DB::transaction(function () use ($taxes) {
                $taxIds = [];
                foreach ($taxes->data->taxes as $key => $tax) {
                    $systemItem = Tax::where('id', $tax->id)->where('shop_id', $this->pos->shop_id)->get()->first();
                    if (is_null($systemItem)) {
                        $response = $this->tax_service->store(['id' => $tax->id, 'shop_id' => $this->pos->shop_id, 'name' => $tax->name, 'type' => $tax->type, 'rate' => $tax->rate, 'status' => 1]);
                    } else {
                        $response = $this->tax_service->update(['name' => $tax->name, 'shop_id' => $this->pos->shop_id, 'type' => $tax->type, 'rate' => $tax->rate, 'status' => 1], $tax->id);
                    }
                    $taxIds[] = $tax->id;
                    if ($response->getStatusCode() != 200) {
                        throw new exception("Internal service error");
                    }
                }
                Tax::where('shop_id', $this->pos->shop_id)->whereNotIn('id', $taxIds)->delete();
            });
            return $this->success('Successfully fetched the Taxes');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function createReceipt($input)
    {
        try {
            $order_id = $input['id'];
            $line_items = [];
            $modifiers = [];
            $input_2 = [];
            $payments = [];
            $woocomerce_modifier = null;
            $total = 0;
            $posValue = $this->setPosValues($input['shop_id']);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            if ($input['platform_name'] == 'WOOCOMMERCE') {
                if ($input['shipping_method'] == 'Delivery') {
                    $customer = Customer::where('order_id', $order_id)->where('type', 'Shipping')->first();
                } else {
                    $customer = Customer::where('order_id', $order_id)->where('type', 'Billing')->first();
                }

                if (!is_null($customer) && $customer->remote_id == null) {
                    $customer_input = [
                        'name' => (isset($customer->first_name) ? $customer->first_name : 'abc') . " " . (isset($customer->last_name) ? $customer->last_name : 'cdf'),
                        'address' => (isset($customer->address_1) ? $customer->address_1 : null) . " " . (isset($customer->address_2) ? $customer->address_2 : null),
                        'email' => (isset($customer->email) ? $customer->email : null),
                        'phone_number' => (isset($customer->phone) ? $customer->phone : null),
                        'city' => (isset($customer->city) ? $customer->city : null),
                        'region' => (isset($customer->state) ? $customer->state : null),
                        'postal_code' => (isset($customer->postcode) ? $customer->postcode : null),
                        'country_code' => (isset($customer->country) ? $customer->country : null),
                    ];
                    $response = $this->request_handle_service->sendPostRequstLoyverse($customer_input, $this->loyverse_version . '/customers', $this->loyverse_base_url, $this->headers);
                    $customer_response = json_decode($response->getBody());
                    DB::table('customers')->where('order_id', $order_id)->update(['remote_id' => $customer_response->id]);
                } else {
                    if (!isset($customer_response)) {
                        $customer_response = (object) array();
                    }
                    $customer_response->id = $customer->remote_id;
                }
            }
            foreach ($input['order_items'] as $key => $value) {
                $modifierSet1 = (is_array($value['modifiers']) ? $value['modifiers'] : unserialize($value['modifiers']));
                if (count($modifierSet1) > 0) {
                    if (isset($input['platform_name']) && $input['platform_name'] == 'WOOCOMMERCE') {
                        foreach ($modifierSet1 as $key => $value_2) {
                            $item_2 = Item::where('title', '=', $value_2['title'])->first();
                            $woocomerce_modifier = $value_2['title'];
                            if ($item_2 != null) {
                                $modifiers[] = [
                                    'modifier_option_id' => $item_2->remote_id,
                                ];
                            }
                        }
                    } else {
                        foreach ($modifierSet1 as $key => $value_2) {
                            foreach ($value_2['selected_item'] as $key => $value_3) {
                                $item_2 = EntityDeliveryPlatform::where('external_item_id', $value_3['item_id'])->first();
                                if ($item_2 != null && !is_null($item_2->item) && !is_null($item_2->item->posItems->where('shop_id', $input['shop_id'])->first())) {
                                    $item_2 = $item_2->item->posItems->where('shop_id', $input['shop_id'])->first();
                                    $modifiers[] = [
                                        'modifier_option_id' => $item_2->variant_id,
                                    ];
                                } else {
                                    $item_2 = Item::find($value_3['item_id']);
                                    if ($item_2 != null && !is_null($item_2->posItems->where('shop_id', $input['shop_id'])->first())) {
                                        $item_2 = $item_2->posItems->where('shop_id', $input['shop_id'])->first();
                                        $modifiers[] = [
                                            'modifier_option_id' => $item_2->variant_id,
                                        ];
                                    }
                                }
                            }
                        }
                    }
                }
                $item = EntityDeliveryPlatform::where('external_item_id', $value['item_id'])->first();
                if ($item != null) {
                    $variantId = $item->item->posItems->where('shop_id', $input['shop_id'])->first();
                    if (!is_null($variantId)) {
                        $variantId = $variantId->variant_id;
                    }
                    $line_items[] = [
                        'variant_id' => $variantId,
                        'quantity' => (int) $value['quantity'],
                        'price' => $value['price_per_item'] / 100,
                        'line_modifiers' => $modifiers,
                    ];
                } else {
                    $item = Item::find($value['item_id']);
                    $variantId = $item->posItems->where('shop_id', $input['shop_id'])->first();
                    if (!is_null($variantId)) {
                        $variantId = $variantId->variant_id;
                    }
                    if ($item != null) {
                        if (isset($input['platform_name']) && $input['platform_name'] == 'WOOCOMMERCE') {
                            $total = $total + ($value['price_per_item'] / 100);
                            $line_items[] = [
                                'variant_id' => $variantId,
                                'quantity' => (int) $value['quantity'],
                                'price' => $value['price_per_item'] / 100,
                                'line_modifiers' => $modifiers,
                                'line_note' => $value['item_name'] . (($woocomerce_modifier == null) ? "" : ' - ' . $woocomerce_modifier) . " tax: " . (isset($value['tax']) ? $value['tax'] : 0),
                            ];
                        } else {
                            $total = $total + ($value['price_per_item'] / 100);
                            $line_items[] = [
                                'variant_id' => $variantId,
                                'quantity' => (int) $value['quantity'],
                                'price' => $value['price_per_item'] / 100,
                                'line_modifiers' => $modifiers,
                            ];
                        }

                    }
                }
                $modifiers = [];
            }

            if ($input['platform_name'] == 'WOOCOMMERCE') {
                $payments[] = [
                    "payment_type_id" => PaymentType::where('shop_id', $input['shop_id'])->where('name', 'Woocommerce')->first()->uuid,
                    'paid_at' => '2020-06-26T12:19:44.044Z',
                ];
                if ($input['shipping_method'] == 'Delivery') {
                    $item_1 = Item::where('title', 'Delivery')->first();
                    $line_items[] = [
                        'variant_id' => $item_1->variant_id,
                        'quantity' => 1,
                        'price' => (float) $input['shipping_total'] + (float) $input['shipping_tax'],
                    ];
                }
            } else {
                $platformName = str_replace('_', ' ', ucfirst(strtolower($input['platform_name'])));
                $paymentObj = PaymentType::where('shop_id', $input['shop_id'])->where('name', $platformName)->first();
                if (is_null($paymentObj)) {
                    $paymentObj = PaymentType::where('shop_id', $input['shop_id'])->where('name', 'Cash')->first();
                }
                $payments[] = [
                    "payment_type_id" => $paymentObj->uuid,
                    'paid_at' => DateTimeUtility::getDateTimeFormat($input['created_at'], 'Y-m-d') . 'T' . DateTimeUtility::getDateTimeFormat($input['created_at'], 'H:i:s') . '.044Z',
                ];
            }

            if (count($line_items) > 0) {
                $storeId = (is_null($item->item->posItems) ? null : $item->item->posItems->where('shop_id', $input['shop_id'])->first());
                //$storeId = $item->item->posItems->where('shop_id', $input['shop_id'])->first();
                if (!is_null($storeId)) {
                    $storeId = $storeId->store_id;
                }
                if ($input['platform_name'] == 'WOOCOMMERCE') {
                    if ($input['shipping_method'] == 'Delivery') {
                        $input_2 = [
                            'store_id' => $storeId,
                            "source" => 'Delivergate',
                            'note' => "Delivery : " . 'Shipping Address: ' . $customer->address_1 . ',' . $customer->address_2 . " city: " . $customer->city . ' state: ' . $customer->state . ' postcode: ' . $customer->postcode . ' Country: ' . $customer->country,
                            'line_items' => $line_items,
                            'payments' => $payments,
                            'customer_id' => (isset($customer_response->id) ? $customer_response->id : null),
                        ];
                    } else {
                        $input_2 = [
                            'store_id' => $storeId,
                            "source" => 'Delivergate',
                            "total_tax" => $input['total_tax'],
                            'line_items' => $line_items,
                            'payments' => $payments,
                            'note' => 'Collection',
                            'customer_id' => (isset($customer_response->id) ? $customer_response->id : null),
                        ];
                    }
                } else {
                    $input_2 = [
                        'store_id' => $storeId,
                        "source" => 'Delivergate',
                        //   "total_tax" => $input['total_tax'],
                        'line_items' => $line_items,
                        'payments' => $payments,
                        'note' => $input['platform_name'],
                    ];

                }

                $response = $this->request_handle_service->sendPostRequstLoyverse($input_2, $this->loyverse_version . '/receipts', $this->loyverse_base_url, $this->headers);
                $receipt = json_decode($response->getBody());
                $this->addReceipt($receipt, $order_id, $input['shop_id']);
                return $this->success('receipt', $receipt);
            } else {
                return $this->noContent('No items to create receipt');
            }

        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function createRefund($order_id)
    {
        try {
            $receipt = Receipt::where('order_id', $order_id['order_id'])->where('receipt_type', 'SALE')->first();
            $posValue = $this->setPosValues($receipt->shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }

            $line_items = [];
            $modifiers = [];
            $input_2 = [];
            $payments = [];
            foreach ($receipt->receiptItems as $key => $value) {
                $line_items[] = [
                    'id' => $value->item_id,
                    'quantity' => $value->quantity,
                ];
            }
            $input_2 = [
                "source" => 'Delivergate',
                'line_items' => $line_items,
            ];
            $response = $this->request_handle_service->sendPostRequstLoyverse($input_2, $this->loyverse_version . '/receipts/' . $receipt->receipt_id . '/refund', $this->loyverse_base_url, $this->headers);
            $refund = json_decode($response->getBody());

            $this->addReceipt($refund, $order_id['order_id'], $receipt->shop_id);
            return $this->success('refund', $refund);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function addReceipt($data, $id, $shop_id = null)
    {
        DB::transaction(function () use ($data, $id, $shop_id) {
            $receipt = new Receipt;
            $receipt->shop_id = $shop_id;
            $receipt->receipt_id = $data->receipt_number;
            $receipt->order_id = $id;
            $receipt->receipt_type = $data->receipt_type;
            $receipt->refund_for = $data->refund_for;
            $receipt->save();

            foreach ($data->line_items as $key => $value) {
                $receipt_item = new ReceiptItem;
                $receipt_item->item_id = $value->id;
                $receipt_item->quantity = $value->quantity;
                $receipt_item->receipt_id = $receipt->id;
                $receipt_item->save();
            }
        });
    }

    public function getPaymentsTypes($shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendGetRequst(null, $this->loyverse_version . '/payment_types', $this->loyverse_base_url, $this->headers);
            $playment_types = json_decode($response->getBody());
            return $this->success('playment_types', $playment_types);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function fetchPaymentsTypes($shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $payment_types = $this->getPaymentsTypes($shop_id);
            $payment_types = json_decode($payment_types->getContent());
            DB::transaction(function () use ($payment_types) {
                $uuids = [];
                foreach ($payment_types->data->payment_types as $key => $payment_type) {
                    $uuids[] = $payment_type->id;
                    $systemItem = PaymentType::where('uuid', $payment_type->id)->first();
                    if (is_null($systemItem)) {
                        $response = $this->payment_service->store(['uuid' => $payment_type->id, 'shop_id' => $this->pos->shop_id, 'name' => $payment_type->name, 'type' => $payment_type->type, 'status' => 1]);
                    } else {
                        $response = $this->payment_service->update(['name' => $payment_type->name, 'shop_id' => $this->pos->shop_id, 'type' => $payment_type->type, 'status' => 1], $payment_type->id);
                    }
                    if ($response->getStatusCode() != 200) {
                        throw new exception("Internal service error");
                    }
                }
                PaymentType::where('shop_id', $this->pos->shop_id)->whereNotIn('uuid', $uuids)->delete();
            });
            return $this->success('Successfully fetched Payments');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function fetchAll($shop_id, $update_modifiers = true)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $this->fetchPaymentsTypes($shop_id);
            $this->fetchPosModifiers($shop_id, $update_modifiers);
            $this->fetchPosTaxes($shop_id);
            $this->fetchPosCategories($shop_id);
            $this->fetchPosItems($shop_id);
            $deliveryPlatforms = explode(',', config('common.fetchable_platforms'));
            foreach ($deliveryPlatforms as $value) {
                //$this->modifier_service->syncPosModifiers($value);
            }
            return $this->success('Successfully fetched All');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function createRemoteItem($data, $shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $catId = null;
            if (isset($data['category_id'])) {
                $posCategory = PosCategory::where('category_id', $data['category_id'])->where('pos_id', $this->pos->id)->get();
                if (count($posCategory)>0) {
                    $catId = $posCategory->first()->remote_id;
                }
            }
            $input = [
                'item_name' => $data['item_name'],
                'category_id' => $catId,
                'variants' => [[
                                    'sku' => $data['sku'],
                                    'default_pricing_type' => 'FIXED',
                                    'default_price' => (float)($data['price'])
                                ]],
            ];
            if (isset($data['type']) && $data['type']=='UPDATE') {
                $posItem = PosItem::where('pos_id', $this->pos->id)->where('shop_id', $this->pos->shop_id)->where('item_id_id', $data['item_id'])->get();
                if (count($posItem)>0) {
                    $posItem = $posItem->first();
                    $input['id'] = $posItem->pos_item_id;
                    $input['variants'][0]['variant_id'] = $posItem->variant_id;
                }
            }
            $response = $this->request_handle_service->sendPostRequstLoyverse($input, $this->loyverse_version . '/items', $this->loyverse_base_url, $this->headers);
            if (is_array($response) || $response->getStatusCode()!=200) {
                return $this->error('Something went wrong');
            }
            $item = json_decode($response->getBody());
            foreach ($item->variants as $key => $variant) {
                $posItemResponse = $this->item_service->storeOrUpdatePosItem(['pos_id' => $this->pos->id, 'shop_id' => $this->pos->shop_id, 'item_id_id' => $data['item_id'], 'title' => $item->item_name, 'pos_item_id' => $item->id, 'handle' => $item->handle, 'reference_id' => $item->reference_id, 'track_stock' => $item->track_stock, 'sold_by_weight' => $item->sold_by_weight, 'is_composite' => $item->is_composite, 'use_production' => $item->use_production, 'form' => $item->form, 'color' => $item->color, 'available_for_sale' => $variant->stores[0]->available_for_sale, 'variant_id' => $variant->variant_id, 'store_id' => $variant->stores[0]->store_id, 'cost' => $variant->cost, 'reference_variant_id' => $variant->reference_variant_id, 'barcode' => $variant->barcode, 'purchase_cost' => $variant->purchase_cost, 'default_pricing_type' => $variant->default_pricing_type, 'default_price' => (float)($data['price']), 'stores' => $variant->stores, 'loyverse_item' => 'true', 'tax_ids' => $item->tax_ids]);
            }
            return $this->success('Successfully created a remote item');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function createRemoteCategory($data, $shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $input = [
                'name'  => $data['name'],
                'color' => "RED"
            ];
            if (isset($data['type']) && $data['type']=='UPDATE') {
                $posCategory = PosCategory::where('pos_id', $this->pos->id)->where('category_id', $data['category_id'])->get();
                if (count($posCategory)>0) {
                    $posCategory = $posCategory->first();
                    $input['id'] = $posCategory->remote_id;
                }
            }
            $response = $this->request_handle_service->sendPostRequstLoyverse($input, $this->loyverse_version . '/categories', $this->loyverse_base_url, $this->headers);
            if (is_array($response) || $response->getStatusCode()!=200) {
                return $this->error('Something went wrong');
            }
            $category = json_decode($response->getBody());
            $this->category_service->storeOrUpdatePosCategory(['remote_id' => $category->id, 'category_id' => $data['category_id'], 'pos_id' => $this->pos->id, 'title' => $category->name, 'sub_title' => '', 'image_path' => null, 'parent_id' => 0, 'description' => '', 'status' => 1]);
            return $this->success('Successfully created a remote category');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function createRemoteModifier($data, $shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $options = [];
            foreach ($data['modifier_options'] as $key => $option) {
                $item = Item::find($option['id']);
                if (!is_null($item)) {
                    $options[] = ['name' => $item->title, 'price' => (float)($option['price'])];
                }
            }
            $input = [
                'name'  => $data['name'],
                'modifier_options' => $options
            ];
            if (isset($data['type']) && $data['type']=='UPDATE') {
                $posModifier = Modifier::where('source_type_id', $this->pos->id)->where('source_type', 'POS')->where('modifier_group_id', $data['modifier_id'])->get();
                if (count($posModifier)>0) {
                    $posModifier = $posModifier->first();
                    $input['id'] = $posModifier->remote_id;
                    foreach ($input['modifier_options'] as $key => $modOption) {
                        if (count($posModifier->options->where('name', $modOption['name']))>0) {
                            $modOption['id'] = $posModifier->options->where('name', $modOption['name'])->first()->remote_id;
                            $input['modifier_options'][$key] = $modOption;
                        }
                    }
                }
            }
            $response = $this->request_handle_service->sendPostRequstLoyverse($input, $this->loyverse_version . '/modifiers', $this->loyverse_base_url, $this->headers);
            if (is_array($response) || $response->getStatusCode()!=200) {
                return $this->error('Something went wrong');
            }
            $modifier = json_decode($response->getBody());

            $this->modifier_service->storeOrUpdatePosModifiers(['pos_id' => $this->pos->id, 'source_type_id' => $this->pos->id, 'shop_id' => $this->pos->shop_id, 'remote_id' => $modifier->id, 'title' => $modifier->name, 'modifier_options' => $modifier->modifier_options, 'loyverse' => "true", 'min_selection' => null, 'max_selection' => null, 'description' => null, 'delivery_platform' => null, 'status' => 1, 'platform' => 'LOYVERSE', 'update_modifiers' => true, 'platform_type' => 'POS', 'modifier_group_id' => $data['modifier_id']]);
            return $this->success('Successfully created a remote category');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }
}
