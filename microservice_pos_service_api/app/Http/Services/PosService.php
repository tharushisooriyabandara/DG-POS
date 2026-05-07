<?php

namespace App\Http\Services;

use App\Http\Helpers\CommonHelper;
use App\Http\Helpers\DateTimeUtility;
use App\Http\Models\Configuration;
use App\Http\Models\MainMenu;
use App\Http\Models\Pos;
use App\Http\Models\ShopMainMenu;
use App\Http\Services\MenuService;
use App\Http\Services\Pos\EposService;
use App\Http\Services\Pos\LoyverseService;
use App\microservice_delivergate_api\Models\Shop;
use App\microservice_delivergate_api\Services\BaseService as BaseService;
use App\microservice_delivergate_api\Services\RequestHandleService;
use Exception;
use Illuminate\Support\Facades\DB;

class PosService extends BaseService
{
    private $client;
    private $request_handle_service;
    private $ePosService;
    private $loyversePosService;

    public function __construct()
    {
        $this->client = new \GuzzleHttp\Client();
        $this->request_handle_service = new RequestHandleService;
        $this->ePosService = new EposService;
        $this->loyversePosService = new LoyverseService;
    }

    public function getApproval($data)
    {
        try {
            $status = 'accepted' /*'denied'*//*'cancel'*/;
            /*if ($status=='accepted') {
            $url = $data['accept_url'];
            $response = $this->request_handle_service->postRequst(['reason' => 'accepted'], $url, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            } else if ($status == 'denied') {
            $url = $data['deny_url'];
            $response = $this->request_handle_service->postRequst(['reason' =>
            [
            "explanation" => "Cannot serve",
            ],
            ], $url, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            } else {
            $url = $data['cancel_url'];
            $response = $this->request_handle_service->postRequst(['reason' => 'RESTAURANT_TOO_BUSY',
            ], $url, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            }*/
            return $this->success();
        } catch (Exception $e) {
            return $this->error();
        }
    }

    public function index($franchise = null, $shop = null)
    {
        try {
            $poses = [];
            if ($franchise == null && $shop == null) {
                $poses = Pos::all();
            } elseif ($shop != null) {
                $poses = Pos::where('shop_id', $shop)->get();
            } elseif ($franchise != null) {
                $outlets = Shop::where('franchise_id', $franchise)->get()->pluck('id');
                if (count($outlets) > 0) {
                    $poses = Pos::whereIn('shop_id', $outlets)->get();
                }
            }
            $posList = [];
            foreach ($poses as $key => $pos) {
                $pos->parameters = unserialize($pos->parameters);
                $pos->parameter_values = unserialize($pos->parameter_values);
                $pos->franchiseName = $pos->outlet->franchise->name;
                $pos->franchiseId = (int) $pos->outlet->franchise->id;
                $pos->outletName = $pos->outlet->name;
                $pos->type = $pos->name;
                $pos->status = ($pos->status == 1 ? "Active" : 'Inactive');
                $posList[] = $pos;
            }
            return $this->success('POS List', $posList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getPosPagination($franchise = null, $shop = null, $page = null, $query = null)
    {
        try {
            $poses = [];
            if ($franchise == null && $shop == null) {
                $poses = Pos::where('name', 'LIKE', '%'.$query.'%')->paginate(10);
            } elseif ($shop != null) {
                $poses = Pos::where('name', 'LIKE', '%'.$query.'%')->where('shop_id', $shop)->paginate(10);
            } elseif ($franchise != null) {
                $outlets = Shop::where('franchise_id', $franchise)->get()->pluck('id');
                if (count($outlets) > 0) {
                    $poses = Pos::where('name', 'LIKE', '%'.$query.'%')->whereIn('shop_id', $outlets)->paginate(10);
                }
            }
            $posList = [];
            foreach ($poses as $key => $pos) {
                $pos->makeVisible(['parameters', 'parameter_values']);
                $pos->parameters = unserialize($pos->parameters);
                $pos->parameter_values = unserialize($pos->parameter_values);
                $pos->franchiseName = $pos->outlet->franchise->name;
                $pos->franchiseId = (int) $pos->outlet->franchise->id;
                $pos->outletName = $pos->outlet->name;
                $pos->type = $pos->name;
                $pos->status = ($pos->status == 1 ? "Active" : 'Inactive');
                $posList[] = $pos;
            }
            $data = ['currentPage' => $poses->currentPage(), 'lastPage' => $poses->lastPage(), 'nextPage' => (($poses->currentPage() == $poses->lastPage()) ? null : ($poses->currentPage() + 1)), 'previousPage' => ($poses->currentPage() == 1 ? null : ($poses->currentPage() - 1)), 'poses' => $posList];
            return $this->success('POS List', $data);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function create($data)
    {
        try {
            $platformResponse = $this->request_handle_service->getRequst(null, '/api/v1/admin/platform/' . $data['platform_id'], null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platform = (json_decode($platformResponse->getBody()))->data;
            $pos = new Pos;
            $pos->name = $platform->name;
            $pos->platform_id = $data['platform_id'];
            $pos->parameters = serialize($platform->parameters);
            $pos->parameter_values = serialize($data['parameters']);
            $pos->shop_id = $data['shop_id'];
            $pos->franchise_id = $data['franchise_id'];
            $pos->status = ($data['status'] == "Active" ? 1 : 0);
            DB::transaction(function () use ($pos, $data) {
                $pos->save();
                Pos::where('id', '!=', $pos->id)->where('franchise_id', $data['franchise_id'])->where('shop_id', $data['shop_id'])->where('status', 1)->update(['status' => 0]);
                CommonHelper::userLog(null, ['description' => 'Created POS named "' . $pos->name . '"', 'event' => 'create', 'subject_type' => 'pos', 'subject_id' => $pos->id]);
                $response = $this->statusCheck(['POS' => $pos->name, 'shop_id' => $pos->shop_id]);
                if (is_array($response) || $response->getStatusCode() != 200) {
                    throw new Exception("Invalid pos values provided.", 1);
                }
            });
            return $this->success('Successfully created the pos for the selected outlet.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            if ($e->getMessage()=='Invalid pos values provided.') {
                return $this->badRequest($e->getMessage());
            }
            return $this->error();
        }
    }

    public function show($id)
    {
        try {
            $pos = Pos::find($id);
            if (is_null($pos)) {
                return $this->notFound('Pos not found');
            }
            $pos->makeVisible(['parameters', 'parameter_values']);
            $pos->parameters = unserialize($pos->parameters);
            $pos->parameter_values = unserialize($pos->parameter_values);
            return $this->success('Pos', $pos);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function update($data, $id)
    {
        try {
            $pos = Pos::find($id);
            if (is_null($pos)) {
                return $this->notFound('Pos not found');
            }
            $pos->platform_id = $data['platform_id'];
            $pos->parameter_values = serialize($data['parameters']);
            $pos->shop_id = $data['shop_id'];
            $pos->franchise_id = $data['franchise_id'];
            $pos->status = ($data['status'] == "Active" ? 1 : 0);
            DB::transaction(function () use ($pos, $data) {
                $pos->save();
                CommonHelper::userLog(null, ['description' => 'Updated POS', 'event' => 'update', 'subject_type' => 'pos', 'subject_id' => $pos->id]);
                $response = $this->statusCheck(['POS' => $pos->name, 'shop_id' => $pos->shop_id]);
                if (is_array($response) || $response->getStatusCode() != 200) {
                    throw new Exception("Invalid pos values provided.", 1);
                }
            });
            return $this->success('Successfully updated the pos.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            if ($e->getMessage()=='Invalid pos values provided.') {
                return $this->badRequest($e->getMessage());
            }
            return $this->error();
        }
    }

    public function setRestaurantStatus($data)
    {
        try {
            $shop = Shop::find($data['id']);
            if (!is_null($shop)) {
                $shop->order_status = $data['status'];
                $shop->save();
                return $this->success('Successfully updated the status');
            }
            return $this->notFound('Not found');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function getRestaurantStatus($id)
    {
        try {
            $shop = Shop::find($id);
            if (!is_null($shop)) {
                return $this->success('Status', ['status' => $shop->order_status]);
            }
            return $this->notFound('Not found');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    /*public function getAutoAcceptStatus($id)
    {
    try {
    $platformResponse = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform/'.$data['platform_id'], null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
    $platform = (json_decode($platformResponse->getBody()))->data;
    $conf = CommonHelper::getConfiguration('auto_accept', $id);
    if (!is_null($conf)) {
    return $this->success('Status', ['status' => $conf->value]);
    }
    return $this->notFound('Not found');
    } catch (Exception $e) {
    $this->loggerError($e, $this, __FUNCTION__, __LINE__);
    return $this->error();
    }
    }

    public function setAutoAcceptStatus($data)
    {
    try {
    $conf = CommonHelper::getConfiguration('auto_accept', $data['shop_id']);
    if (!is_null($conf)) {
    $conf->value = $data['status'];
    $conf->save();
    return $this->success('Status updated', ['status' => $conf->value]);
    }
    return $this->notFound('Not found');
    } catch (Exception $e) {
    $this->loggerError($e, $this, __FUNCTION__, __LINE__);
    return $this->error();
    }
    }*/

    public function getPosCategories($data)
    {
        try {
            if ($data['POS'] == 'EPOS') {
                return $this->ePosService->getPosCategories($data['shop_id']);
            } elseif ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->getPosCategories($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getPosCategoryById($data, $id)
    {
        try {
            if ($data['POS'] == 'EPOS') {
                return $this->ePosService->getPosCategoryById($id, $data['shop_id']);
            } elseif ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->getPosCategoryById($id, $data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function fetchPosCategories($data)
    {
        try {
            if ($data['POS'] == 'EPOS') {
                return $this->ePosService->fetchPosCategories($data['shop_id']);
            } elseif ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->fetchPosCategories($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getPosItems($data)
    {
        try {
            if ($data['POS'] == 'EPOS') {
                return $this->ePosService->getPosItems($data['shop_id']);
            } elseif ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->getPosItems($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getPosItemById($data, $id)
    {
        try {
            if ($data['POS'] == 'EPOS') {
                return $this->ePosService->getPosItemById($id, $data['shop_id']);
            } elseif ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->getPosItemById($id, $data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function fetchPosItems($data)
    {
        try {
            if ($data['POS'] == 'EPOS') {
                return $this->ePosService->fetchPosItems($data['shop_id']);
            } elseif ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->fetchPosItems($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getTenderTypes($data)
    {
        try {
            if ($data['POS'] == 'EPOS') {
                return $this->ePosService->getTenderTypes($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function syncWithPos($shop_id = null, $update_modifiers = true)
    {
        try {
            $failures = 0;
            $successes = 0;
            if (!is_null($shop_id)) {
                $poses = Pos::where('status', 1)->where('shop_id', $shop_id)->get();
            } else {
                $poses = Pos::where('status', 1)->get();
            }
            foreach ($poses as $key => $pos) {
                if ($pos->name == 'EPOS') {
                    $response = $this->ePosService->syncWithPos($pos->shop_id);
                    if ($response->getStatusCode() == 200) {
                        $pos->last_synced_time = DateTimeUtility::getDateTimeFormat('now', 'Y-m-d H:i:s');
                        $pos->save();
                        $shop = Shop::find($pos->shop_id);
                        $shop->last_pos_synced = DateTimeUtility::getDateTimeFormat('now', 'Y-m-d H:i:s');
                        $shop->save();
                        $successes++;
                    } else {
                        $failures++;
                    }
                    //return $response;
                } elseif ($pos->name == 'LOYVERSE') {
                    $response = $this->loyversePosService->fetchAll($pos->shop_id, $update_modifiers);
                    if ($response->getStatusCode() == 200) {
                        $pos->last_synced_time = DateTimeUtility::getDateTimeFormat('now', 'Y-m-d H:i:s');
                        $pos->save();
                        $shop = Shop::find($pos->shop_id);
                        $shop->last_pos_synced = DateTimeUtility::getDateTimeFormat('now', 'Y-m-d H:i:s');
                        $shop->save();
                        $successes++;
                    } else {
                        $failures++;
                    }
                    //return $response;
                }
            }
            if ($failures > 0 && $successes == 0) {
                return $this->error('Couldn\'t sync any pos data.');
            } elseif ($failures == 0 && $successes > 0) {
                return $this->success('Successfully synced all pos data.');
            } elseif ($failures == 0 && $successes == 0) {
                return $this->success('Nothing synced.');
            } else {
                return $this->error('Partially synced the pos data.');
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getPosModifiers($data)
    {
        try {
            if ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->getPosModifiers($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getPosModifierById($data, $id)
    {
        try {
            if ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->getPosModifierById($id, $data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function fetchPosModifiers($data)
    {
        try {
            if ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->fetchPosModifiers($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getPosTaxes($data)
    {
        try {
            if ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->getPosTaxes($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }
    // This need to be removed if not using
    // public function getPosTaxById($data, $id)
    // {
    //     try {
    //         if ($data['POS'] == 'LOYVERSE') {
    //             return $this->loyversePosService->getPosTaxById($id, $data['shop_id']);
    //         }
    //     } catch (Exception $e) {
    //         $this->loggerError($e, $this, __FUNCTION__, __LINE__);
    //         return $this->error();
    //     }
    // }

    public function fetchPosTaxes($data)
    {
        try {
            if ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->fetchPosTaxes($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function createReceipt($data)
    {
        try {
            return $this->loyversePosService->createReceipt($data);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function createRefund($data)
    {
        return $this->loyversePosService->createRefund($data);
    }

    public function getPaymentsTypes($data)
    {
        try {
            if ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->getPaymentsTypes($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function fetchPaymentsTypes($data)
    {
        try {
            if ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->fetchPaymentsTypes($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function fetchAll($data)
    {
        try {
            if ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->fetchAll($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getStoreDetails($id)
    {
        try {
            $shop = Shop::where('code', $id)->get();
            if (count($shop) == 0) {
                return $this->notFound('Shop not found');
            }
            $shop = $shop->first();
            $shop->franchise_name = $shop->franchise->name;
            $riderPlatform = $shop->riderPlatforms()->whereIn('status', ['active', 'Active'])->first();
            $shop->has_rider_platform_enabled = (int) !is_null($riderPlatform);
            return $this->success('Store details', $shop);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function updateStoreDetails($data)
    {
        try {
            $configs = ['STORE_NAME', 'STORE_ADDRESS', 'STORE_BRANCH'];
            foreach ($configs as $key => $config) {
                if (isset($data['value_' . $config])) {
                    $configuration = Configuration::firstOrNew([
                        'key' => $config,
                    ]);
                    $configuration->value = $data['value_' . $config];
                    $configuration->save();
                }
            }
            return $this->success('Successfully stored the details');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getPosTypes()
    {
        try {
            $platformResponse = $this->request_handle_service->getRequst(null, '/api/v1/admin/platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
            $platform = (json_decode($platformResponse->getBody()))->data;
            $posList = [];
            foreach ($platform as $key => $pos) {
                if ($pos->type == "POS") {
                    $pos->platform_id = $pos->id;
                    $pos->parameters = $pos->parameters;
                    $pos->parameter_values = $this->createObject($pos->parameters);
                    $posList[] = $pos;
                }
            }
            return $this->success('POS Type List', $posList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function createObject($data)
    {
        $result = [];
        foreach ($data as $key => $value) {
            $result[$value] = "";
        }
        return $result;
    }

    public function delete($id)
    {
        try {
            $pos = Pos::find($id)->delete();
            CommonHelper::userLog(null, ['description' => 'Deleted POS named "' . $pos->name . '"', 'event' => 'delete', 'subject_type' => 'pos', 'subject_id' => $pos->id]);
            return $this->success('POS deleted successfully', "deleted id - " . $id);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function getActiveShopMenu($id, $query = '')
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
            $mainMenuArray = [];
            foreach ($mainMenus as $key => $mainMenu) {
                $mainMenuArray[$mainMenu->id] = $mainMenu;
            }
            $currentMenus = Shop::find($id);
            $mainMenuIds = $mainMenus->pluck('id')->toArray();
            $categoryOrder = array_merge($selected_menu_ids, $mainMenuIds);
            uksort($mainMenuArray, function ($key1, $key2) use ($categoryOrder) {
                return ((array_search($key1, $categoryOrder) > array_search($key2, $categoryOrder)) ? 1 : -1);
            });

            $allMenuData = ['Snoozed' => [], 'Available' => []];
            $itemIds = [];
            foreach ($mainMenuArray as $key => $mainMenu) {
                $categories = $menuService->getPreviewItems($mainMenu->id, null, true, $query, $id, true);
                $categories = json_decode($categories->getContent());
                if ($categories->code==200) {
                    foreach ($categories->data as $key1 => $typeData) {
                        foreach ($typeData as $key2 => $item) {
                            if (!in_array($item->id, $itemIds)) {
                                $allMenuData[$key1][] = $item;
                                $itemIds[] = $item->id;
                            }
                        }
                    }
                }
            }
            return $this->success('Items', $allMenuData);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function updateServiceAvailability($data)
    {
        try {
            DB::transaction(function () use ($data) {
                $availability = [];
                if (isset($data['availability'])) {
                    foreach ($data['availability'] as $key => $av) {
                        $availability[] = ['day_of_week' => $key, 'availability' => true, 'time_periods' => [['start_time' => $av['from'], 'end_time' => $av['to']]]];
                    }
                }
                Shop::WhereIn('id', $data['shop_ids'])->update(['service_availability' => serialize($availability)]);
                $mainMenu = MainMenu::find($data['main_menu']);
                /*We are getting all shop_ids for update
                if (isset($data['type']) && $data['type']=='update') {
                }*/
                $mainMenu->shops()->detach($data['shop_ids']);
                $mainMenu->shops()->attach($data['shop_ids']);
            });
            return $this->success('Updated the availability');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function statusCheck($data)
    {
        try {
            if ($data['POS'] == 'EPOS') {
                return $this->ePosService->getTenderTypes($data['shop_id']);
            } elseif ($data['POS'] == 'LOYVERSE') {
                return $this->loyversePosService->getPosTaxes($data['shop_id']);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function createRemoteItem($data)
    {
        try {
            $poses = Pos::where('status', 1)->get();
            $success = 0;
            $error = 0;
            foreach ($poses as $key => $pos) {
                if ($pos->name == 'EPOS') {
                    $response = $this->ePosService->createRemoteItem($data, $pos->shop_id);
                } elseif ($pos->name == 'LOYVERSE') {
                    $response = $this->loyversePosService->createRemoteItem($data, $pos->shop_id);
                }
                if ($response->getStatusCode()==200) {
                    $success++;
                } else {
                    $error++;
                }
            }
            if ($error==0) {
                return $this->success('Successfully created the remote items', ['success' => $success, 'error' => $error]);
            } else {
                return $this->success('Couldn\'t create the remote items', ['success' => $success, 'error' => $error]);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function createRemoteCategory($data)
    {
        try {
            $poses = Pos::where('status', 1)->get();
            $success = 0;
            $error = 0;
            foreach ($poses as $key => $pos) {
                if ($pos->name == 'EPOS') {
                    $response = $this->ePosService->createRemoteCategory($data, $pos->shop_id);
                } elseif ($pos->name == 'LOYVERSE') {
                    $response = $this->loyversePosService->createRemoteCategory($data, $pos->shop_id);
                }
                if ($response->getStatusCode()==200) {
                    $success++;
                } else {
                    $error++;
                }
            }
            if ($error==0) {
                return $this->success('Successfully created the remote items', ['success' => $success, 'error' => $error]);
            } else {
                return $this->success('Couldn\'t create the remote items', ['success' => $success, 'error' => $error]);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function createRemoteModifier($data)
    {
        try {
            $poses = Pos::where('status', 1)->get();
            $success = 0;
            $error = 0;
            foreach ($poses as $key => $pos) {
                if ($pos->name == 'EPOS') {
                    $response = $this->ePosService->createRemoteModifier($data, $pos->shop_id);
                } elseif ($pos->name == 'LOYVERSE') {
                    $response = $this->loyversePosService->createRemoteModifier($data, $pos->shop_id);
                }
                if ($response->getStatusCode()==200) {
                    $success++;
                } else {
                    $error++;
                }
            }
            if ($error==0) {
                return $this->success('Successfully created the remote items', ['success' => $success, 'error' => $error]);
            } else {
                return $this->error('Couldn\'t create the remote items', ['success' => $success, 'error' => $error]);
            }
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error();
        }
    }

    public function locationUpdate($data)
    {
        try {
            $shop = [];
            DB::transaction(function () use ($data, &$shop) {
                $shop = Shop::findOrFail($data['shop']['shopID']);
                $shop->latitude = $data['shop']['latitude'];
                $shop->longitude = $data['shop']['longitude'];
                $shop->country_Code = $data['shop']['country_Code'];
                $shop->timezone = (isset($data['shop']['timezone'])?$data['shop']['timezone']:$shop->timezone);
                $shop->currency = (isset($data['shop']['currency'])?$data['shop']['currency']:$shop->currency);
                $shop->currency_code = (isset($data['shop']['currency_code'])?$data['shop']['currency_code']:$shop->currency_code);
                $shop->google_location_url = (isset($data['shop']['google_location_url'])?$data['shop']['google_location_url']:$shop->google_location_url);
                $shop->postal_code = (isset($data['shop']['postal_code'])?$data['shop']['postal_code']:$shop->postal_code);
                $shop->save();
            });
            return $this->success('Shop Location Updated', $shop);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error($e->getMessage());
        }
    }
}
