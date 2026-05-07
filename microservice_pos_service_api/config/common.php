<?php

return [
    'epos_key' => env('EPOS_KEY'),
    'epos_secret' => env('EPOS_SECRET'),
    'epos_auth_token' => env('EPOS_AUTH_TOKEN'),
    'epos_base_url' => env('EPOS_BASE_URL'),
    'fetchable_platforms' => explode(',', env('FETCHABLE_PLATFORMS', '1,2'))
];
