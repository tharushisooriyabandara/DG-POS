<?php

namespace App\Console\Commands;

use DB;
use Config;
use Exception;
use CommonHelper;
use App\Http\Models\Item;
use App\Http\Models\Images;
use Illuminate\Console\Command;
use App\Http\Services\ItemService;
use App\Http\Services\ImageService;
use App\microservice_delivergate_api\Services\BaseService;

class ResizeImageSizes extends Command
{
    /**
     * The name and signature of the console command.
     *
     * @var string
     */
    protected $signature = 'resize:itemimages {code?}';

    /**
     * The console command description.
     *
     * @var string
     */
    protected $description = 'Resize item images';

    /**
     * Create a new command instance.
     *
     * @return void
     */

    private $itemService;
    private $imageService;
    public function __construct()
    {
        parent::__construct();

        $this->imageService = new ImageService;
        $this->itemService = new ItemService;
    }

    /**
     * Execute the console command.
     *
     * @return mixed
     */

    public function resizeImages($tenantCode)
    {
        $items = Item::whereNotNull('image_url')->get();
        $resizedCount = 0;
        foreach ($items as $key => $item) {
            try {
                if (count($item->images)==0) {
                    $imagePaths = $this->imageService->resizeAndUploadImageToCloudByUrl($item->image_url, 'products');
                    $item->image_url = $imagePaths['medium'];
                    $item->save();
                    $group = CommonHelper::generateRandomCode(8);
                    foreach ($imagePaths as $imkey => $path) {
                        $itemImage = Images::firstOrNew([
                            'type' => 'ITEM',
                            'type_id' => $item->id,
                            'size' => $imkey,
                        ]);
                        $itemImage->group = $group;
                        $itemImage->path = $path;
                        $itemImage->save();
                    }
                    $resizedCount++;
                }
            } catch (Exception $e) {
                \Log::error('Unable to resize the image for '.$item->title.' in '. $tenantCode);
            }
        }
        if ($resizedCount>0) {
            \Log::error('Resized '.$resizedCount.' images in '. $tenantCode);
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
            $this->resizeImages($tenant->x_tenant_code);
        }
        return 0;
    }
}
