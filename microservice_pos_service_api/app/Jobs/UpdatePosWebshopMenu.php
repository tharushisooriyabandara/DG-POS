<?php

namespace App\Jobs;

use DB;
use Config;
use Illuminate\Bus\Queueable;
use App\Http\Models\ShopMainMenu;
use App\Http\Services\MenuService;
use App\Jobs\UpdateSnoozeItemList;
use Illuminate\Queue\SerializesModels;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;
use App\microservice_delivergate_api\Models\Shop;

class UpdatePosWebshopMenu implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    public $mainMenuId;
    public $shopId;
    public $tenantCode;
    /**
     * Create a new job instance.
     *
     * @return void
     */
    public function __construct($data)
    {
        $this->mainMenuId = $data['mainMenuId'];
        $this->shopId = $data['shopId'];
        $this->tenantCode = $data['tenantCode'];
    }

    /**
     * Execute the job.
     *
     * @return void
     */
    public function handle()
    {
        Config::set("database.connections.mysql.host", config('applications.master_host'));
        Config::set("database.connections.mysql.port", config('applications.master_port'));
        Config::set("database.connections.mysql.database", config('applications.master_db'));
        Config::set("database.connections.mysql.username", config('applications.master_username'));
        Config::set("database.connections.mysql.password", config('applications.master_password'));
        DB::reconnect();

        $tenant=DB::table('tenants')->where('x_tenant_code', $this->tenantCode)->first();
        Config::set("database.connections.mysql.host", $tenant->host);
        Config::set("database.connections.mysql.port", $tenant->port);
        Config::set("database.connections.mysql.database", $tenant->database_name);
        Config::set("database.connections.mysql.username", $tenant->username);
        Config::set("database.connections.mysql.password", $tenant->password);
        DB::reconnect();

        $_SERVER['HTTP_X_TENANT_CODE'] = $tenant->x_tenant_code;
        $menuService = new MenuService;    
        $dp  = DB::table('delivery_platform')->where('outlet_id', $this->shopId)->whereIn('platform_id', [6,8])->whereNull('deleted_at')->get();
        $selectedMenu = null;
        $selectedMenuList = [];
        if (!is_null($this->mainMenuId)) {
            $selectedMenu = $this->mainMenuId;
            $selectedMenuList[] = $this->mainMenuId;
        }
        foreach ($dp as $key => $deliveryPlatform) {
            if (is_null($this->mainMenuId)) {
                $selectedMenu = $deliveryPlatform->selected_menu;
                $selectedMenuList[] = $selectedMenu;
            }
            $menuService->updateWebshopMenu($selectedMenu, $deliveryPlatform->id, true, '', $this->shopId, time(), true);
        }
        $selectedMenuList = array_unique($selectedMenuList);
        $shopIds = ShopMainMenu::whereIn('main_menu_id', $selectedMenuList)->distinct()->pluck('shop_id')->toArray();
        if (!empty($shopIds)) {
            $chunks = array_chunk($shopIds, 15);
            foreach ($chunks as $chunk) {
                UpdateSnoozeItemList::dispatch([
                    'shopIds' => array_values($chunk),
                    'tenantCode' => $this->tenantCode,
                ])->onConnection('sqs3');
            }
        }
    }
}
