<?php

namespace App\Console\Commands;

use DB;
use Config;
use Exception;
use CommonHelper;
use DateTimeUtility;
use Illuminate\Console\Command;
use App\Http\Services\ItemService;
use App\Http\Models\EntityDeliveryPlatform;
use App\microservice_delivergate_api\Models\Shop;
use App\microservice_delivergate_api\Services\BaseService;
use App\microservice_delivergate_api\Services\RequestHandleService;

class MakeSnoozedItemsAvailable extends Command
{
    /**
     * The name and signature of the console command.
     *
     * @var string
     */
    protected $signature = 'snooze:update {code?}';

    /**
     * The console command description.
     *
     * @var string
     */
    protected $description = 'Unsnooze items';

    /**
     * Create a new command instance.
     *
     * @return void
     */

    private $request_handle_service;
    private $itemService;
    public function __construct()
    {
        parent::__construct();
        $this->itemService = new ItemService;
        $this->request_handle_service = new RequestHandleService;
    }

    /**
     * Execute the console command.
     *
     * @return mixed
     */

    public function makeSnoozedItemsAvailable($tenantCode)
    {
        $time = DateTimeUtility::getDateTimeFormat('Now', 'Y-m-d H:i:s');
        $shops = Shop::where('status', 'active')->get();
        $response = $this->request_handle_service->getRequst(null, '/api/v1/admin/delivery-platform', null, 'delivery_service', CommonHelper::getXTenantCode($_SERVER));
        $deliveryPlatforms = (json_decode($response->getBody()))->data;

        foreach ($shops as $key => $shop) {
            $platforms = collect($deliveryPlatforms)->where('outlet_id', $shop->id)->whereIn('platform_id', Config::get('common.fetchable_platforms'));
            $platformIds = $platforms->pluck('id')->toArray();
            $entityItems = EntityDeliveryPlatform::where('available_from', '<', $time)->whereNotNull('entity_id')->where('available', 0)/*->whereIn('delivery_platform_id', $platformIds)*/->pluck('entity_id')->toArray();
            if (count($entityItems)>0) {
                $rtn = $this->itemService->snoozeItems($shop->id, ['item_ids' => $entityItems, 'available_from' => null]);
        	    \Log::info($tenantCode.' - '.$shop->id.' / '.$shop->name.' - Unsnoozed the items '.DateTimeUtility::getDateTimeFormat('now', 'd/m/Y h:i a'));
            }
        }
    }
    public function handle()
    {
        $code = $this->argument('code');
        $commom_service = new RequestHandleService;
        Config::set("database.connections.mysql.host", config('applications.master_host'));
        Config::set("database.connections.mysql.port", config('applications.master_port'));
        Config::set("database.connections.mysql.database", config('applications.master_db'));
        Config::set("database.connections.mysql.username", config('applications.master_username'));
        Config::set("database.connections.mysql.password", config('applications.master_password'));
        DB::reconnect();
        if ($code=='') {
            $tenants=DB::table('tenants')->get();
        } else {
            $tenants=DB::table('tenants')->where('x_tenant_code', $code)->get();
        }
        foreach ($tenants as $key => $tenant) {
            Config::set("database.connections.mysql.host", $tenant->host);
            Config::set("database.connections.mysql.port", $tenant->port);
            Config::set("database.connections.mysql.database", $tenant->database_name);
            Config::set("database.connections.mysql.username", $tenant->username);
            Config::set("database.connections.mysql.password", $tenant->password);
            DB::reconnect();

            $_SERVER['HTTP_X_TENANT_CODE'] = $tenant->x_tenant_code;
            $this->makeSnoozedItemsAvailable($tenant->x_tenant_code);
        }
        return 0;
    }
}
