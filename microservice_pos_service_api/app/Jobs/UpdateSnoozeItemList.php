<?php

namespace App\Jobs;

use DB;
use Config;
use Illuminate\Bus\Queueable;
use App\Http\Services\MenuService;
use Illuminate\Queue\SerializesModels;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;

class UpdateSnoozeItemList implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    public $shopIds;
    public $tenantCode;

    /**
     * Create a new job instance.
     *
     * @return void
     */
    public function __construct($data)
    {
        $this->shopIds = $data['shopIds'];
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

        $tenant = DB::table('tenants')->where('x_tenant_code', $this->tenantCode)->first();
        
        Config::set("database.connections.mysql.host", $tenant->host);
        Config::set("database.connections.mysql.port", $tenant->port);
        Config::set("database.connections.mysql.database", $tenant->database_name);
        Config::set("database.connections.mysql.username", $tenant->username);
        Config::set("database.connections.mysql.password", $tenant->password);
        DB::reconnect();

        $_SERVER['HTTP_X_TENANT_CODE'] = $tenant->x_tenant_code;

        $menuService = new MenuService;
        foreach ($this->shopIds as $key => $shopId) {
            $menuService->updateSnoozeItemListJson($shopId);
        }
    }
}
