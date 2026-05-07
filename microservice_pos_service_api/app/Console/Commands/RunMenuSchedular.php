<?php

namespace App\Console\Commands;

use DB;
use Config;
use Exception;
use CommonHelper;
use DateTimeUtility;
use Illuminate\Console\Command;
use App\Http\Services\ItemService;
use App\Http\Models\MainMenuSchedular;
use App\Http\Models\EntityDeliveryPlatform;
use App\microservice_delivergate_api\Models\Shop;
use App\microservice_delivergate_api\Services\BaseService;
use App\microservice_delivergate_api\Services\RequestHandleService;

class RunMenuSchedular extends Command
{
    /**
     * The name and signature of the console command.
     *
     * @var string
     */
    protected $signature = 'schedule:main-menu {code?}';

    /**
     * The console command description.
     *
     * @var string
     */
    protected $description = 'Schedule main menu publishing';

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

    public function scheduleMainMenu($tenantCode)
    {
        $slackurl = 'api/v1/send-slack-message';
        $pasttime = DateTimeUtility::getDateTimeFormat('-12 hours', 'Y-m-d H:i:s');
        $futuretime = DateTimeUtility::getDateTimeFormat('+12 hours', 'Y-m-d H:i:s');
        $MainMenuSchedulars = MainMenuSchedular::whereBetween('publishable_date', [$pasttime, $futuretime])->where('status', 1)->orderBy('publishable_date', 'DESC')->get();
        $hasPublished = false;
        foreach ($MainMenuSchedulars as $key => $schedule) {
            if (!is_null($schedule->mainMenu)) {
                $shopTime = DateTimeUtility::getDateTimeFormatWithTimeZone('now', 'Y-m-d H:i:s', $schedule->mainMenu->masterOutlet->timezone);
                if ($schedule->mainMenu->status=='ACTIVE' && DateTimeUtility::datePassed($shopTime, $schedule->publishable_date) && !$hasPublished) {
                    $url = '/api/v1/remote/sync-pos-items-with-delivery-platform';
                    $details = ['main_menu' => $schedule->main_menu_id, 'delivery_platform' => (is_null($schedule->platform_ids)?[]:unserialize($schedule->platform_ids))];
                    $syncWDPResponse = $this->request_handle_service->postRequst($details, $url, 'delivery_service', $tenantCode);
                    if (!is_array($syncWDPResponse) && $syncWDPResponse->getStatusCode() == 200) {
                        \Log::info('Main menu published');
                        $batchId = (json_decode($syncWDPResponse->getBody()))->data;
                        $syncWMaster = $this->request_handle_service->getRequst($schedule->main_menu_id.'?batch_id='.$batchId, '/api/v1/remote/sync-with-master-menu', null, 'delivery_service', $tenantCode);
                        if (!is_array($syncWMaster) && $syncWMaster->getStatusCode() == 200) {
                            $schedule->status = false;
                            $schedule->save();
                            $hasPublished = true;
                            \Log::info('Synced menus with master menu');
                        } else {
                            $response = $this->request_handle_service->postRequst(['from' => ucfirst($tenantCode), 'message' => 'We could not sync the menu with the master menu which is supposed to be published at '.DateTimeUtility::getDateTimeFormatWithTimeZone($schedule->publishable_date, 'd/m/Y h:i a', $schedule->mainMenu->masterOutlet->timezone).' due to some issue in the menu. Please inform the admin ASAP. Tenant code: '.$tenantCode.', Menu: '.$schedule->mainMenu->name.', Menu ID: '.$schedule->mainMenu->id], $slackurl, 'notification_service', $tenantCode);
                            \Log::error('Could not sync with master menu');
                        }
                    } else {
                        $response = $this->request_handle_service->postRequst(['from' => ucfirst($tenantCode), 'message' => 'We could not publish the menu which is supposed to be published at '.DateTimeUtility::getDateTimeFormatWithTimeZone($schedule->publishable_date, 'd/m/Y h:i a', $schedule->mainMenu->masterOutlet->timezone).' due to some issue in the menu. Please inform the admin ASAP. Tenant code: '.$tenantCode.', Menu: '.$schedule->mainMenu->name.', Menu ID: '.$schedule->mainMenu->id], $slackurl, 'notification_service', $tenantCode);
                        \Log::error('Could not publish the main menu');
                    }
                } elseif ($schedule->mainMenu->status=='ACTIVE' && DateTimeUtility::datePassed($shopTime, $schedule->publishable_date) && $hasPublished) {
                    $schedule->status = false;
                    $schedule->save();    
                }
            } else {
                $schedule->status = false;
                $schedule->save();
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
            $this->scheduleMainMenu($tenant->x_tenant_code);
        }
        return 0;
    }
}
