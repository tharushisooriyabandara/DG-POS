<?php

namespace App\Http\Services\Pos;

use App\Http\Models\Category;
use App\Http\Models\Configuration;
use App\Http\Models\EntityDeliveryPlatform;
use App\Http\Models\Item;
use App\Http\Models\Pos;
use App\Http\Models\Receipt;
use App\Http\Models\ReceiptItem;
use App\Http\Services\CategoryService;
use App\Http\Services\ItemService;
use App\microservice_delivergate_api\Services\BaseService as BaseService;
use App\microservice_delivergate_api\Services\RequestHandleService;
use Exception;
use Illuminate\Support\Facades\DB;

class EposService extends BaseService
{
    private $pos;
    private $headers;
    private $epos_key;
    private $epos_secret;
    private $item_service;
    private $epos_base_url;
    private $epos_auth_token;
    private $category_service;
    private $request_handle_service;

    public function __construct()
    {
        $this->item_service = new ItemService;
        $this->category_service = new CategoryService;
        $this->request_handle_service = new RequestHandleService;

        $this->pos = null;
        $this->epos_base_url = null;
        $this->epos_key = null;
        $this->epos_secret = null;
        $this->epos_auth_token = null;
        $this->headers = null;
    }

    public function setPosValues($shop_id)
    {
        $this->pos = Pos::where('shop_id', $shop_id)->where('name', 'EPOS')->get()->first();
        if (is_null($this->pos)) {
            return 'NOT_FOUND';
        }
        $paramValues = unserialize($this->pos->parameter_values);
        $paramValues = json_decode(json_encode($paramValues));
        $this->epos_base_url = $paramValues->EPOS_BASE_URL;
        $this->epos_key = $paramValues->EPOS_KEY;
        $this->epos_secret = $paramValues->EPOS_SECRET;
        $this->epos_auth_token = $paramValues->EPOS_AUTH_TOKEN;
        $this->headers = [
            'Authorization' => 'Basic ' . $this->epos_auth_token,
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
            $response = $this->request_handle_service->sendGetRequst(null, '/api/v4/Category', $this->epos_base_url, $this->headers);
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
            $response = $this->request_handle_service->sendGetRequst(null, '/api/v4/Category/' . $id, $this->epos_base_url, $this->headers);
            $category = json_decode($response->getBody());
            return $this->success('Category', $category);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function fetchPosCategories($shop_id)
    {
        // TODO need to check with Kajan
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $categories = $this->getPosCategories($shop_id);
            $categories = json_decode($categories->getContent());
            DB::transaction(function () use ($categories) {
                foreach ($categories->data as $key => $category) {
                    $systemCategory = Category::where('title', $category->Name)->get()->first();
                    if (is_null($systemCategory)) {
                        $response = $this->category_service->store(['title' => $category->Name, 'sub_title' => '', 'image_path' => $category->ImageUrl, 'parent_id' => 0, 'description' => $category->Description, 'status' => 1]);
                    }
                    if (isset($response) && $response->getStatusCode() != 200) {
                        throw new Exception('Issues in fetching parent categories.', 1);
                    }
                    if (is_null($systemCategory)) {
                        $systemCategory = Category::where('title', $category->Name)->get()->first();
                    }
                    $this->category_service->storeOrUpdatePosCategory(['remote_id' => $category->Id, 'category_id' => $systemCategory->id, 'pos_id' => $this->pos->id, 'title' => $category->Name, 'sub_title' => '', 'image_path' => $category->ImageUrl, 'parent_id' => 0, 'description' => $category->Description, 'status' => 1]);

                    if (count($category->Children) > 0) {
                        foreach ($category->Children as $key1 => $subCategory) {
                            $systemSubCategory = Category::where('title', $subCategory->Name)->get()->first();
                            if (is_null($systemSubCategory)) {
                                $response = $this->category_service->store(['title' => $subCategory->Name, 'sub_title' => '', 'image_path' => $subCategory->ImageUrl, 'parent_id' => $subCategory->ParentId, 'description' => $subCategory->Description, 'status' => 1]);
                            }
                            if (isset($response) && $response->getStatusCode() != 200) {
                                throw new Exception('Issues in fetching child categories.', 1);
                            }
                            if (is_null($systemSubCategory)) {
                                $systemSubCategory = Category::where('title', $subCategory->Name)->get()->first();
                            }
                            $this->category_service->storeOrUpdatePosCategory(['remote_id' => $subCategory->Id, 'category_id' => $systemSubCategory->id, 'pos_id' => $this->pos->id, 'title' => $subCategory->Name, 'sub_title' => '', 'image_path' => $subCategory->ImageUrl, 'parent_id' => $subCategory->ParentId, 'description' => $subCategory->Description, 'status' => 1]);
                        }
                    }
                }
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
            $statResponse = $this->request_handle_service->sendGetRequst(null, '/api/v4/Product/Stats', $this->epos_base_url, $this->headers);
            $statResponse = json_decode($statResponse->getBody());
            $pages = $statResponse->TotalProducts / 200;
            $pages = (ceil($pages) + 1);
            $items = [];
            for ($i = 1; $i < $pages; $i++) {
                $response = $this->request_handle_service->sendGetRequst(null, '/api/v4/Product?page=' . $i, $this->epos_base_url, $this->headers);
                $tmpArray = json_decode($response->getBody());
                $items = array_merge($items, $tmpArray);
            }
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
            $response = $this->request_handle_service->sendGetRequst(null, '/api/v4/Product/' . $id, $this->epos_base_url, $this->headers);
            $item = json_decode($response->getBody());
            return $this->success('Items', $item);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function fetchPosItems($shop_id)
    {
        // TODO Need to check this with Kajan
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $items = $this->getPosItems($shop_id);
            $items = json_decode($items->getContent());
            DB::transaction(function () use ($items) {
                $isEmptyItems = (count(Item::all()) == 0);
                foreach ($items->data as $key => $item) {
                    $imagePath = '';
                    if (count($item->ProductImages) > 0) {
                        $image = $item->ProductImages;
                        $imagePath = (isset($image[0]->ImageUrl['url']) ? $image[0]->ImageUrl['url'] : ((isset($image[0]->ImageUrl) && !is_array($image[0]->ImageUrl)) ? $image[0]->ImageUrl : ''));
                    }
                    $tmpPrice = round($item->SalePrice, 2);
                    $systemItem = Item::where('title', $item->Name)->where('price', $tmpPrice)->get()->first();

                    if (is_null($systemItem)) {
                        $response = $this->item_service->store(['pos_id' => $this->pos->id, 'source' => 'POS', 'title' => $item->Name, 'description' => $item->Description, 'image_url' => $imagePath, 'tax' => (isset($item->SalePriceTaxGroup->TaxRates[0]) ? $item->SalePriceTaxGroup->TaxRates[0]->Percentage : 0), 'price' => $tmpPrice, 'sku' => $item->Sku, 'status' => 1, 'category_id' => $item->CategoryId]);
                    }
                    if (is_null($systemItem)) {
                        $systemItem = Item::where('title', $item->Name)->where('price', $tmpPrice)->get()->first();
                    }
                    $response = $this->item_service->storeOrUpdatePosItem(['pos_id' => $this->pos->id, 'shop_id' => $this->pos->shop_id, 'title' => $item->Name, 'item_id_id' => $systemItem->id, 'pos_item_id' => $item->Id, 'handle' => null, 'reference_id' => null, 'track_stock' => null, 'sold_by_weight' => null, 'is_composite' => null, 'use_production' => null, 'form' => null, 'color' => null, 'available_for_sale' => null, 'variant_id' => null, 'store_id' => null, 'cost' => $item->CostPrice, 'reference_variant_id' => null, 'barcode' => $item->Barcode, 'purchase_cost' => 0, 'default_pricing_type' => null, 'default_price' => $tmpPrice]);
                    if ($response->getStatusCode() != 200) {
                        throw new Exception('Issues in fetching items.', 1);
                    }
                }
            });
            return $this->success('Successfully fetched the items');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    /**
     *  this use to create a transaction in ePos
     *  sample data set
     *  {
     *   "Device": "string",
     *   "DateTime": "2020-11-04T06:30:59.378Z",
     *   "StatusId": 1,
     *   "ReferenceCode": "lahiru",
     *   "TransactionItems": [
     *       {
     *       "ProductId": 27459655,
     *       "UnitPrice": 1399.00,
     *       "UnitPriceExcTax": 0,
     *       "CostPrice": 1388.00,
     *       "Quantity": 5
     *       }
     *   ],
     *   "Tenders": [
     *       {
     *       "TenderTypeId": 72439,
     *       "Amount": 6995,
     *       "ChangeGiven": 0,
     *       "IsCashback": true
     *       }
     *   ],
     *   "AdjustStock": true
     * }
     */

    /*  Statuses
    ...............
    Completed   1
    Held    7
    Ordered 8*/
    public function createTransactions($data)
    {
        $data = json_encode($data);
        $data = json_decode($data);
        $posValue = $this->setPosValues($data->shop_id);
        if ($posValue == 'NOT_FOUND') {
            return $this->notFound();
        }
        $transactionItems = [];
        $Amount = 0;
        foreach ($data->order_items as $key => $item) {
            $tmpItemId = $item->item_id;
            $entityItem = EntityDeliveryPlatform::where('plu', $item->item_id)->where('delivery_platform_id', $data->platform_id)->get();
            if (count($entityItem) > 0) {
                $entityItem = $entityItem->first();
                if (!is_null($entityItem->item->posItems->where('shop_id', $data->shop_id)) && count($entityItem->item->posItems->where('shop_id', $data->shop_id)) > 0) {
                    $tmpItemId = $entityItem->item->posItems->where('shop_id', $data->shop_id)->first()->pos_item_id;
                }
            }

            $modifierPrice = 0;
            if (is_string($item->modifiers)) {
                $item->modifiers = unserialize($item->modifiers);
            }
            if (!is_null($item->modifiers) && count($item->modifiers) > 0) {
                foreach ($item->modifiers as $keyMod => $modifier) {
                    $modifierPrice += $modifier->price_per_item;
                }
            }

            $pricePerItem = $modifierPrice + $item->price_per_item;

            $tmpItem = [
                "ProductId" => $tmpItemId,
                "UnitPrice" => ($pricePerItem / 100),
                "Quantity" => $item->quantity,
            ];
            $Amount += $pricePerItem * $item->quantity;
            $transactionItems[] = $tmpItem;
        }
        $serviceCharge = $data->total_amount - $Amount;
        $input = [
            'Id' => $data->id,
            'Device' => 'Delivergate',
            /*'DateTime' => DateTimeUtility::getDateTimeFormat($data->created_at, 'Y-m-d').'T'.DateTimeUtility::getDateTimeFormat($data->created_at, 'H:i:s.u').'Z',*/
            'ReferenceCode' => $data->id,
            'TransactionItems' => $transactionItems,
            'ServiceCharge' => ($serviceCharge > 0 ? ($serviceCharge / 100) : 0),
            'StatusId' => (in_array($data->status, ['COMPLETED', 'ACCEPTED']) ? 1 : 8),
            'AdjustStock' => true,
            "Tenders" => [
                [
                    "TenderTypeId" => $data->delivery_platform->tender_types,
                    "Amount" => ($data->total_amount / 100),
                ],
            ],
        ];

        if (count(Receipt::where('order_id', $data->id)->get()) > 0) {
            return $this->success('Transaction already created.');
        }

        try {
            $response = $this->request_handle_service->sendPostRequst($input, '/api/v4/Transaction', $this->epos_base_url, ['Authorization' => 'Basic ' . $this->epos_auth_token, 'Content-Type' => 'application/json']);

            if (!is_array($response) && $response->getStatusCode() == 201) {
                $eposData = json_decode($response->getBody());
                $id = $data->id;
                DB::transaction(function () use ($eposData, $id, $data) {
                    $receipt = new Receipt;
                    $receipt->receipt_id = $eposData->Id;
                    $receipt->order_id = $id;
                    $receipt->receipt_type = 'SALE';
                    /*$receipt->refund_for = $eposData->refund_for;*/
                    $receipt->save();

                    foreach ($data->order_items as $key => $item) {
                        $receipt_item = new ReceiptItem;
                        $receipt_item->item_id = $item->item_id;
                        $receipt_item->quantity = $item->quantity;
                        $receipt_item->receipt_id = $receipt->id;
                        $receipt_item->save();
                    }
                });
                return $this->success('Created the transaction');
            } else {
                $this->loggerError(null, $this, __FUNCTION__, __LINE__, 'Couldn\'t create the transaction for order #' . $data->id);
                return $this->error('Something went wrong');
            }

        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function updateTransactionsById($id, $data)
    {
        $data = json_encode($data);
        $data = json_decode($data);
        $posValue = $this->setPosValues($data->shop_id);
        if ($posValue == 'NOT_FOUND') {
            return $this->notFound();
        }
        $receipt = Receipt::where('order_id', $data->id)->get();
        $receipt = $receipt->first();
        if (!is_null($receipt) && in_array($data->status, ['CANCELED', 'DENIED'])) {
            $response = $this->deleteTransaction($receipt->receipt_id, $data->shop_id);
            if (!is_array($response) && $response->getStatusCode() == 204) {
                $receipt->receipt_type = 'REFUND';
                $receipt->save();
                return $this->success('Deleted the transaction');
            } else {
                $this->loggerError(null, $this, __FUNCTION__, __LINE__, 'Couldn\'t delete the transaction for order #' . $data->id);
                return $this->error('Something went wrong');
            }
        }
        $transactionItems = [];
        $Amount = 0;
        foreach ($data->order_items as $key => $item) {
            $tmpItemId = $item->item_id;
            $entityItem = EntityDeliveryPlatform::where('plu', $item->item_id)->where('delivery_platform_id', $data->platform_id)->get();
            if (count($entityItem) > 0) {
                $entityItem = $entityItem->first();
                if (!is_null($entityItem->item->posItems->where('shop_id', $data->shop_id)) && count($entityItem->item->posItems->where('shop_id', $data->shop_id)) > 0) {
                    $tmpItemId = $entityItem->item->posItems->where('shop_id', $data->shop_id)->first()->pos_item_id;
                }
            }

            $modifierPrice = 0;
            if (!is_null($item->modifiers) && count($item->modifiers) > 0) {
                foreach ($item->modifiers as $keyMod => $modifier) {
                    $modifierPrice += $modifier->price_per_item;
                }
            }

            $pricePerItem = $modifierPrice + $item->price_per_item;
            $tmpItem = [
                "ProductId" => $tmpItemId,
                "UnitPrice" => ($pricePerItem / 100),
                "Quantity" => $item->quantity,
            ];
            $Amount += $pricePerItem * $item->quantity;
            $transactionItems[] = $tmpItem;
        }

        $serviceCharge = $data->total_amount - $Amount;
        $input = [
            'Id' => $data->id,
            'Device' => 'Delivergate',
            /*'DateTime' => DateTimeUtility::getDateTimeFormat($data->created_at, 'Y-m-d').'T'.DateTimeUtility::getDateTimeFormat($data->created_at, 'H:i:s.SSS').'Z',*/
            'ReferenceCode' => $data->id,
            'ServiceCharge' => ($serviceCharge > 0 ? ($serviceCharge / 100) : 0),
            'TransactionItems' => $transactionItems,
            'StatusId' => (in_array($data->status, ['COMPLETED', 'ACCEPTED']) ? 1 : (in_array($data->status, ['CANCELED', 'DENIED']) ? 7 : 8)),
            'AdjustStock' => true,
            "Tenders" => [
                [
                    "TenderTypeId" => $data->delivery_platform->tender_types,
                    "Amount" => ($data->total_amount / 100),
                ],
            ],
        ];

        try {
            if (!is_null($receipt)) {
                $response = $this->request_handle_service->sendPutRequst($input, '/api/v4/Transaction/' . $receipt->receipt_id, $this->epos_base_url, ['Authorization' => 'Basic ' . $this->epos_auth_token, 'Content-Type' => 'application/json']);
                if (!is_array($response) && $response->getStatusCode() == 200) {
                    $eposData = json_decode($response->getBody());
                    DB::transaction(function () use ($eposData, $receipt, $data) {
                        $receipt->receipt_type = (in_array($data->status, ['CANCELED', 'DENIED']) ? 'REFUND' : 'SALE');
                        $receipt->save();
                    });
                    return $this->success('Updated the transaction');
                } else {
                    $this->loggerError(null, $this, __FUNCTION__, __LINE__, 'Couldn\'t create the transaction for order #' . $data->id);
                    return $this->error('Something went wrong');
                }
            } else {
                $this->loggerError(null, $this, __FUNCTION__, __LINE__, 'Receipt not found');
                return $this->notFound('Receipt not found for order #' . $data->id);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function deleteTransaction($id, $shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendDeleteRequst($id, '/api/v4/Transaction', $this->epos_base_url, ['Authorization' => 'Basic ' . $this->epos_auth_token]);
            return $response;

        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    /**
     * this function use to get single transaction by id, barcode, reference code, by table or latest
     * input example
     * @param string $type     -- barcode/reference_code/latest/by_table
     * @param string $id
     * if $id not null and $type null then comes data by relatd to the given id
     * if you want to get latest record, only set $type. no need to set $id
     * if $id null and $type null then retrive all transactions.
     */
    public function getSingleTransactions($id = null, $type = null, $shop_id = null)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            if ($type == 'barcode') {
                $endPoint = '/api/v4/Transaction/Barcode';
            } else if ($type == 'reference_code') {
                $endPoint = '/api/v4/Transaction/ReferenceCode';
            } else if ($type == 'latest') {
                $endPoint = '/api/v4/Transaction/GetLatest';
                $id = '';
            } else if ($type == 'by_table') {
                $endPoint = '/api/v4/Transaction/Table';
            } else {
                $endPoint = '/api/v4/Transaction';
            }
            $response = $this->request_handle_service->sendGetRequst($id, $endPoint, $this->epos_base_url, ['Authorization' => 'Basic ' . $this->epos_auth_token, 'Content-Type' => 'application/json']);
            if ($response->getStatusCode() != 200) {
                return $response;
            }
            return $this->success('Transaction', json_decode($response->getBody()));

        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getTransactions($shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendGetRequst(null, '/api/v4/Transaction', $this->epos_base_url, ['Authorization' => 'Basic ' . $this->epos_auth_token, 'Content-Type' => 'application/json']);
            if ($response->getStatusCode() != 200) {
                return $response;
            }
            return $this->success('All Transaction list', json_decode($response->getBody()));
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function getTenderTypes($shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendGetRequst(null, '/api/v4/TenderType', $this->epos_base_url, ['Authorization' => 'Basic ' . $this->epos_auth_token, 'Content-Type' => 'application/json']);
            $tenderTypes = json_decode($response->getBody());
            return $this->success('Tender type list', $tenderTypes);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function createTenderType($data, $shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $response = $this->request_handle_service->sendPostRequst($data, '/api/v4/TenderType', $this->epos_base_url, ['Authorization' => 'Basic ' . $this->epos_auth_token, 'Content-Type' => 'application/json']);
            if (!is_array($response) && $response->getStatusCode() == 201) {
                return $this->success('Created Tender types', json_decode($response->getBody()));
            } else {
                return $this->error('Something went wrong');
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function syncWithPos($shop_id)
    {
        try {
            $posValue = $this->setPosValues($shop_id);
            if ($posValue == 'NOT_FOUND') {
                return $this->notFound();
            }
            $categoryResponse = $this->fetchPosCategories($shop_id);
            if ($categoryResponse->getStatusCode() != 200) {
                return $this->error('Couldn\'t fetch the E-pos categories.');
            }
            $itemResponse = $this->fetchPosItems($shop_id);
            if ($itemResponse->getStatusCode() != 200) {
                return $this->error('Couldn\'t fetch the E-pos items.');
            }
            $tenderResponse = $this->createTenderTypes($shop_id);
            if ($tenderResponse->getStatusCode() != 200) {
                return $this->error('Couldn\'t fetch the E-pos tender types.');
            }

            return $this->success('Successfully fetched the pos items and categories.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function createTenderTypes($shop_id)
    {
        $posValue = $this->setPosValues($shop_id);
        if ($posValue == 'NOT_FOUND') {
            return $this->notFound();
        }
        try {
            $types = [
                [
                    "Id" => 80193,
                    "Name" => "UBER_EATS",
                    "Description" => "Uber Eats orders",
                    "ClassificationId" => 1,
                ],
                [
                    "Id" => 80194,
                    "Name" => "DELIVEROO",
                    "Description" => "Deliveroo orders",
                    "ClassificationId" => 1,
                ],
                [
                    "Id" => 80195,
                    "Name" => "WOOCOMMERCE",
                    "Description" => "Woocommerce orders",
                    "ClassificationId" => 1,
                ],
            ];

            /*$platformResponse = $this->request_handle_service->getRequst(null, '/api/v1/admin/platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platforms = (json_decode($platformResponse->getBody()))->data;
            $platformList = [];
            foreach ($platforms as $key => $platform) {
            $platformList[$platform->id] = $platform->logo;
            }

            $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $deliveryPlatforms = (json_decode($response->getBody()))->data;
            $deliveryPlatformList = [];
            foreach ($deliveryPlatforms as $key => $deliveryPlatform) {
            if (in_array($deliveryPlatform->platform_id, Config::get('common.fetchable_platforms'))) {
            $deliveryPlatformList[$deliveryPlatform->id] = ['id' => $deliveryPlatform->id, 'name' => ucfirst(strtolower(str_replace('_', ' ', $deliveryPlatform->name))), 'logo' => $platformList[$deliveryPlatform->platform_id]];
            }
            }*/

            foreach ($types as $key => $type) {
                if (count(Configuration::where('key', $type['Name'])->get()) == 0) {
                    $response = $this->createTenderType([$type], $shop_id);
                    if ($response->getStatusCode() == 200) {
                        $response = (json_decode($response->getContent()))->data;
                        if (is_array($response) && count($response) > 0) {
                            $config = new Configuration;
                            $config->key = $response[0]->Name;
                            $config->value = $response[0]->Id;
                            $config->save();
                        }
                    }
                }
            }
            return $this->success('Successfully created tender types.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function createRemoteItem($data, $shop_id)
    {
        return $this->success();
    }

    public function createRemoteCategory($data, $shop_id)
    {
        return $this->success();
    }
}
