# Laravel Passport Authentication Setup

This document explains how to set up Laravel Passport OAuth2 authentication for the POS WPF application.

## Overview

The application now uses Laravel Passport for OAuth2 authentication. The authentication flow works as follows:

1. User enters email and PIN
2. Application authenticates with Laravel Passport using client credentials flow
3. Laravel Passport returns a bearer token
4. Application uses the bearer token to authenticate with the main API

## Configuration

### 1. Client Credentials

The application requires Laravel Passport client credentials to authenticate:

- **Client ID**: `3`
- **Client Secret**: `QHk3rzyz6ePU2FKn9PvufPZOsBYp4AxWNzzPe5zs`

### 2. Settings Configuration

The client credentials are stored in the application settings. To configure your own credentials:

1. Copy `Properties/Settings.settings.template` to `Properties/Settings.settings`
2. Replace the placeholder values with your actual Laravel Passport client credentials:
   - Replace `YOUR_CLIENT_ID_HERE` with your actual client ID
   - Replace `YOUR_CLIENT_SECRET_HERE` with your actual client secret

### 3. Security

- The `Properties/Settings.settings` file is excluded from Git to prevent committing sensitive credentials
- Always use the template file for new installations
- Never commit actual client credentials to version control

## API Endpoints

### Laravel Passport Authentication
- **Base URL**: `https://user-dev.delivergate.com`
- **Token Endpoint**: `/oauth/token`
- **User Info Endpoint**: `/api/user`

### Authentication Flow

1. **Password Grant**: Used for initial login
   ```
   POST /oauth/token
   Content-Type: application/x-www-form-urlencoded
   
   grant_type=password&
   client_id=YOUR_CLIENT_ID&
   client_secret=YOUR_CLIENT_SECRET&
   username=user@example.com&
   password=user_pin&
   scope=*
   ```

2. **Refresh Token Grant**: Used to refresh expired tokens
   ```
   POST /oauth/token
   Content-Type: application/x-www-form-urlencoded
   
   grant_type=refresh_token&
   client_id=YOUR_CLIENT_ID&
   client_secret=YOUR_CLIENT_SECRET&
   refresh_token=REFRESH_TOKEN&
   scope=*
   ```

## Implementation Details

### Services

- **LaravelPassportService**: Handles OAuth2 authentication with Laravel Passport
- **ApiService**: Updated to use Laravel Passport bearer tokens for API requests
- **TokenValidationService**: Updated to clear Laravel bearer tokens on logout

### Token Storage

The application stores three types of tokens:

1. **LaravelBearerToken**: The OAuth2 bearer token from Laravel Passport
2. **AccessToken**: The main API access token
3. **RefreshToken**: The main API refresh token

### Token Usage

- Laravel Passport bearer tokens are used for all API requests to the main backend
- The application automatically uses the Laravel bearer token when available
- Falls back to the regular access token if Laravel bearer token is not available

## Troubleshooting

### Common Issues

1. **Authentication Failed**: Check that client credentials are correct
2. **Token Expired**: The application should automatically refresh tokens
3. **Network Issues**: Verify connectivity to `https://user-dev.delivergate.com`

### Debug Information

The application logs authentication information to the console:
- Laravel Passport responses
- Token usage (Laravel vs regular tokens)
- Authentication errors

## Migration from Previous Authentication

If migrating from the previous authentication system:

1. The application will automatically use Laravel Passport for new logins
2. Existing tokens will continue to work until they expire
3. After the first Laravel Passport login, all subsequent requests will use the new authentication flow

## Security Best Practices

1. **Never commit credentials**: Always use the template file
2. **Rotate secrets regularly**: Update client secrets periodically
3. **Monitor token usage**: Check logs for authentication issues
4. **Secure storage**: The application stores tokens in user settings (encrypted on Windows) 