<?php

namespace App\Console;

use App\microservice_delivergate_api\Services\BaseService;
use Illuminate\Console\Scheduling\Schedule;
use Illuminate\Foundation\Console\Kernel as ConsoleKernel;

class Kernel extends ConsoleKernel
{
    /**
     * The Artisan commands provided by your application.
     *
     * @var array
     */
    protected $commands = [
        Commands\MakeSnoozedItemsAvailable::class,
        Commands\PublishDeliverooMenu::class,
        Commands\RunMenuSchedular::class,
    ];

    /**
     * Define the application's command schedule.
     *
     * @param  \Illuminate\Console\Scheduling\Schedule  $schedule
     * @return void
     */
    protected function schedule(Schedule $schedule)
    {
        $schedule->command('snooze:update')->cron('*/15 * * * *');
        $schedule->command('publish:deliveroo-menu')->cron('*/5 * * * *');
        $schedule->command('schedule:main-menu')->cron('*/5 * * * *');
        $schedule->call(function () {
            $base_service = new BaseService;
            $base_service->updateServiceToken();
        })->yearlyOn(12, 25, '01:00');
    }

    /**
     * Register the commands for the application.
     *
     * @return void
     */
    protected function commands()
    {
        $this->load(__DIR__ . '/Commands');

        require base_path('routes/console.php');
    }
}
