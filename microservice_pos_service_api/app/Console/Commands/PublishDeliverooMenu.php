<?php

namespace App\Console\Commands;

use DB;
use Config;
use Exception;
use CommonHelper;
use DateTimeUtility;
use App\Http\Models\MainMenu;
use App\Http\Models\ItemPrice;
use Illuminate\Console\Command;
use App\microservice_delivergate_api\Models\Shop;
use App\microservice_delivergate_api\Services\BaseService;
use App\microservice_delivergate_api\Services\RequestHandleService;

class PublishDeliverooMenu extends Command
{
    /**
     * The name and signature of the console command.
     *
     * @var string
     */
    protected $signature = 'publish:deliveroo-menu {code?}';

    /**
     * The console command description.
     *
     * @var string
     */
    protected $description = 'Publish deliveroo menu';

    /**
     * Create a new command instance.
     *
     * @return void
     */
    public function __construct()
    {
        parent::__construct();
    }

    /**
     * Execute the console command.
     *
     * @return mixed
     */

    public function publishDeliverooMenu($tenantCode)
    {
        $requestHandleService = new RequestHandleService;
        $shops = Shop::whereIn('status', ['ACTIVE', 'Active', 'active'])->get();
        foreach ($shops as $key => $shop) {
            $shopMenu = null;
            $day =  strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Now', 'l', $shop->timezone));
            $time =  strtolower(DateTimeUtility::getDateTimeFormatWithTimeZone('Now', 'H:i:s', $shop->timezone));
            $deliveryPlatforms = DB::table('delivery_platform')->where('is_master', 1)->where('platform_id', 1)->whereIn('status', ['active', 'Active'])->get();
            $timmme = null;
            if (count($deliveryPlatforms)>0) {
                foreach ($deliveryPlatforms as $keyDp => $deliveryPlatform) {
                    $mainMenu = MainMenu::find($deliveryPlatform->selected_menu);
                    if (!is_null($mainMenu)) {
                        if (count($mainMenu->menus->where('main_menu_id', $mainMenu->id))==1) {
                            if (!is_null($shop->service_availability)) {
                                $shopMenu = $shop->service_availability;
                            }
                        }
                        if (count($mainMenu->menus->where('main_menu_id', $mainMenu->id))>1) {
                            foreach ($mainMenu->menus->where('main_menu_id', $mainMenu->id) as $keyMenu => $menu) {
                                $itemIds = ItemPrice::where('main_menu_id', $mainMenu->id)->where('delivery_platform_id', $deliveryPlatform->id)->pluck('entity_item_id')->toArray();
                                $menuPlatformItemIds = array_intersect((is_null($menu->item_ids) ? [] : unserialize($menu->item_ids)), $itemIds);
                                if (count($menuPlatformItemIds) > 0) {
                                    $serviceAvailability = collect(unserialize(is_null($shopMenu)?$menu->service_availability:$shopMenu));
                                    $dayAvailability = $serviceAvailability->where('day_of_week', $day)->first();
                                    if (!is_null($dayAvailability)) {
                                        $timeAvailability = collect($dayAvailability['time_periods']);
                                        if (count($timeAvailability->where('start_time', '<=', $time)->where('end_time', '>=', $time))>0 && (is_null($deliveryPlatform->menu_published_at) || (count(DB::table('delivery_platform')->where('id', $deliveryPlatform->id)->whereTime('menu_published_at', '>=', $timeAvailability->first()['start_time'])->whereTime('menu_published_at', '<=', $timeAvailability->first()['end_time'])->get())==0))) {
                                            $responseMaster = $requestHandleService->postRequst(['main_menu' => $mainMenu->id, 'delivery_platform' => [$deliveryPlatform->id], 'sub_menu_id' => $menu->id], '/api/v1/remote/sync-pos-items-with-delivery-platform', 'delivery_service', $tenantCode);
                                            if (!is_array($responseMaster) && $responseMaster->getStatusCode()==200) {
                                                \Log::info($tenantCode.' - '.$deliveryPlatform->name.': Deliveroo master menu published at '.DateTimeUtility::getDateTimeFormat('now', 'd/m/Y h:i a'));
                                                DB::table('delivery_platform')->where('id', $deliveryPlatform->id)->update(['menu_published_at' => DateTimeUtility::getDateTimeFormatWithTimeZone('Now', 'Y-m-d H:i:s', $shop->timezone)]);
                                                $batchId = json_decode($responseMaster->getBody())->data;
                                                $platformResponse = $requestHandleService->getRequst($mainMenu->id.'?batch_id='.$batchId, '/api/v1/remote/sync-with-master-menu', null, 'delivery_service', $tenantCode);
                                                if (!is_array($platformResponse) && $platformResponse->getStatusCode()==200) {
                                                    \Log::info($tenantCode.' - '.$deliveryPlatform->name.': Deliveroo slave menu published at '.DateTimeUtility::getDateTimeFormat('now', 'd/m/Y h:i a'));
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public function handle()
    {
        $code = $this->argument('code');
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
            $this->publishDeliverooMenu($tenant->x_tenant_code);
        }
        return 0;
    }
}
