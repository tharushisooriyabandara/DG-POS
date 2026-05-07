<?php

namespace App\Providers;

use App\microservice_delivergate_api\Models\Policy;
use Illuminate\Foundation\Support\Providers\AuthServiceProvider as ServiceProvider;
use Illuminate\Support\Facades\Schema;
use Laravel\Passport\Passport;

class AuthServiceProvider extends ServiceProvider
{
    /**
     * The policy mappings for the application.
     *
     * @var array
     */
    protected $policies = [
        // 'App\Model' => 'App\Policies\ModelPolicy',
    ];

    /**
     * Register any authentication / authorization services.
     *
     * @return void
     */
    public function boot()
    {
        $this->registerPolicies();
        $data = [];
        if (Schema::hasTable('policy')) {
            $policies = Policy::all();
            foreach ($policies as $policy) {
                $data[$policy->id] = $policy->name;
            }
        }
        Passport::tokensCan($data);
        Passport::routes();
    }
}
