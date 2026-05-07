using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Threading;

namespace POS_UI.Services
{
    public class LaravelPassportService
    {
        private HttpClient _httpClient;
        // Alternative URLs to try if the main one fails:
        // private const string BaseUrl = "https://delivergate.com";
        // private const string BaseUrl = "http://user-dev.delivergate.com";
        // private const string BaseUrl = "https://api.delivergate.com";
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly SettingsService _settingsService;
        private readonly string _baseUrl;
        private readonly string _tenantCode;
        
        // Token caching
        private string _cachedAccessToken;
        private DateTime _cachedTokenExpiry = DateTime.MinValue;
        private readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);

        public LaravelPassportService()
        {
            _baseUrl = EnvironmentService.Instance.Config.Urls.UserApiBaseUrl?.Trim();
            if (string.IsNullOrWhiteSpace(_baseUrl))
            {
                _baseUrl = "https://user-dev.delivergate.com";
            }
            
            // Load client credentials strictly from configuration (appsettings)
            var envClientId = EnvironmentService.Instance.Config.Auth.LaravelClientId;
            var envClientSecret = EnvironmentService.Instance.Config.Auth.LaravelClientSecret;

            if (string.IsNullOrWhiteSpace(envClientId) || string.IsNullOrWhiteSpace(envClientSecret))
            {
                throw new InvalidOperationException("Laravel client credentials are missing in configuration.");
            }

            _clientId = envClientId;
            _clientSecret = envClientSecret;
            
            // Load tenant code from desktop settings file (same as Go API)
            _settingsService = new SettingsService();
            var (tenantCode, outletCode, brandId) = _settingsService.LoadSettings();
            _tenantCode = tenantCode;
            
            // Initialize HttpClient
            InitializeHttpClient();
        }
        
        private void InitializeHttpClient()
        {
            // Dispose old client if exists
            _httpClient?.Dispose();
            
            // Create new HttpClient with proper configuration
            var handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 10,
                UseDefaultCredentials = false,
                AllowAutoRedirect = true
            };
            
            _httpClient = new HttpClient(handler);
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // Reduced from 5 minutes
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.ConnectionClose = false; // Keep-alive
            
            // Add tenant code header (same as Go API)
            if (!string.IsNullOrWhiteSpace(_tenantCode))
            {
                _httpClient.DefaultRequestHeaders.Add("x-tenant-code", _tenantCode);
                Console.WriteLine($"Laravel Passport: Added x-tenant-code header: {_tenantCode}");
            }
            else
            {
                Console.WriteLine("Laravel Passport: Warning - No tenant code found in settings");
            }
        }

        public async Task<string> GetAccessTokenAsync()
        {
            // Check if we have a valid cached token
            await _tokenLock.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTime.UtcNow < _cachedTokenExpiry.AddMinutes(-5))
                {
                    Console.WriteLine("Laravel Passport: Using cached token");
                    return _cachedAccessToken;
                }
            }
            finally
            {
                _tokenLock.Release();
            }
            
            // Try to get new token with retry logic
            return await GetAccessTokenWithRetryAsync();
        }
        
        private async Task<string> GetAccessTokenWithRetryAsync()
        {
            await _tokenLock.WaitAsync();
            try
            {
                // Double-check pattern: another thread might have refreshed the token
                if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTime.UtcNow < _cachedTokenExpiry.AddMinutes(-5))
                {
                    return _cachedAccessToken;
                }
                
                const int maxRetries = 3;
                const int baseDelayMs = 1000; // Start with 1 second
                
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        if (retry > 0)
                        {
                            // Exponential backoff: 1s, 2s, 4s
                            int delayMs = baseDelayMs * (int)Math.Pow(2, retry - 1);
                            Console.WriteLine($"Laravel Passport: Retry {retry}/{maxRetries} after {delayMs}ms delay");
                            await Task.Delay(delayMs);
                            
                            // Reinitialize HttpClient on retry (fixes stale connection issues)
                            Console.WriteLine("Laravel Passport: Reinitializing HTTP client for fresh connection");
                            InitializeHttpClient();
                        }
                        
                        // Try different common Laravel Passport OAuth2 endpoints
                        var possibleEndpoints = new[] { "/oauth/token", "/api/oauth/token", "/auth/token", "/api/auth/token" };
                        
                        Console.WriteLine($"Laravel Passport: Attempting client credentials authentication (Attempt {retry + 1}/{maxRetries})");
                        
                        Exception lastException = null;
                        
                        foreach (var tokenEndpoint in possibleEndpoints)
                        {
                            try
                            {
                                Console.WriteLine($"Laravel Passport: Trying endpoint: {tokenEndpoint}");
                                
                                var formData = new[]
                                {
                                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                                    new KeyValuePair<string, string>("client_id", _clientId),
                                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                                    new KeyValuePair<string, string>("scope", "*")
                                };

                                var content = new FormUrlEncodedContent(formData);
                                
                                Console.WriteLine($"Laravel Passport: Sending request to {_httpClient.BaseAddress}{tokenEndpoint}");
                                
                                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                                var response = await _httpClient.PostAsync(tokenEndpoint, content, cts.Token);
                        
                                Console.WriteLine($"Laravel Passport: Response status: {response.StatusCode}");
                                
                                if (!response.IsSuccessStatusCode)
                                {
                                    var errorContent = await response.Content.ReadAsStringAsync();
                                    Console.WriteLine($"Laravel Passport Error for {tokenEndpoint}: {response.StatusCode} - {errorContent}");
                                    
                                    lastException = new Exception($"Endpoint {tokenEndpoint} failed: {response.StatusCode} - {errorContent}");
                                    continue; // Try next endpoint
                                }

                                var json = await response.Content.ReadAsStringAsync();
                                Console.WriteLine($"Laravel Passport Response: {json}");
                                
                                using var doc = JsonDocument.Parse(json);
                                var accessToken = doc.RootElement.GetProperty("access_token").GetString();
                                
                                // Try to get expiry time (typically 3600 seconds for client credentials)
                                int expiresIn = 3600; // Default 1 hour
                                if (doc.RootElement.TryGetProperty("expires_in", out var expiresInElement))
                                {
                                    expiresIn = expiresInElement.GetInt32();
                                }
                                
                                // Cache the token
                                _cachedAccessToken = accessToken;
                                _cachedTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
                                
                                Console.WriteLine($"Laravel Passport: Success with endpoint {tokenEndpoint}, token expires at {_cachedTokenExpiry:u}");
                                
                                return accessToken;
                            }
                            catch (TaskCanceledException)
                            {
                                Console.WriteLine($"Laravel Passport: Timeout for endpoint {tokenEndpoint}");
                                lastException = new Exception($"Endpoint {tokenEndpoint} timed out");
                                continue; // Try next endpoint
                            }
                            catch (HttpRequestException httpEx)
                            {
                                Console.WriteLine($"Laravel Passport: HTTP error for endpoint {tokenEndpoint}: {httpEx.Message}");
                                lastException = httpEx;
                                continue; // Try next endpoint
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Laravel Passport: Error with endpoint {tokenEndpoint}: {ex.Message}");
                                lastException = ex;
                                continue; // Try next endpoint
                            }
                        }
                        
                        // If we get here, all endpoints failed for this retry
                        if (retry == maxRetries - 1)
                        {
                            // Last retry failed, throw the exception
                            throw lastException ?? new Exception("All Laravel Passport endpoints failed");
                        }
                        
                        // Continue to next retry
                        Console.WriteLine($"Laravel Passport: All endpoints failed on attempt {retry + 1}, will retry");
                    }
                    catch (Exception ex) when (retry < maxRetries - 1)
                    {
                        Console.WriteLine($"Laravel Passport: Attempt {retry + 1} failed: {ex.Message}");
                        // Continue to next retry
                    }
                }
                
                throw new Exception("Laravel Passport: Max retries exceeded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Laravel Passport Service Error: {ex.Message}");
                throw;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public async Task<string> RefreshAccessTokenAsync(string refreshToken)
        {
            const int maxRetries = 3;
            const int baseDelayMs = 1000;
            
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    if (retry > 0)
                    {
                        int delayMs = baseDelayMs * (int)Math.Pow(2, retry - 1);
                        Console.WriteLine($"Laravel Passport Refresh: Retry {retry}/{maxRetries} after {delayMs}ms delay");
                        await Task.Delay(delayMs);
                        
                        // Reinitialize HttpClient on retry
                        InitializeHttpClient();
                    }
                    
                    var tokenEndpoint = "/oauth/token";
                    
                    var formData = new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "refresh_token"),
                        new KeyValuePair<string, string>("client_id", _clientId),
                        new KeyValuePair<string, string>("client_secret", _clientSecret),
                        new KeyValuePair<string, string>("refresh_token", refreshToken),
                        new KeyValuePair<string, string>("scope", "*")
                    };

                    var content = new FormUrlEncodedContent(formData);
                    
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var response = await _httpClient.PostAsync(tokenEndpoint, content, cts.Token);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Laravel Passport Refresh Error: {response.StatusCode} - {errorContent}");
                        
                        if (retry == maxRetries - 1)
                        {
                            throw new Exception($"Token refresh failed: {response.StatusCode} - {errorContent}");
                        }
                        continue; // Retry
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var accessToken = doc.RootElement.GetProperty("access_token").GetString();
                    
                    // Update cached token
                    await _tokenLock.WaitAsync();
                    try
                    {
                        _cachedAccessToken = accessToken;
                        int expiresIn = 3600;
                        if (doc.RootElement.TryGetProperty("expires_in", out var expiresInElement))
                        {
                            expiresIn = expiresInElement.GetInt32();
                        }
                        _cachedTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
                    }
                    finally
                    {
                        _tokenLock.Release();
                    }
                    
                    Console.WriteLine("Laravel Passport: Token refresh successful");
                    return accessToken;
                }
                catch (Exception ex) when (retry < maxRetries - 1)
                {
                    Console.WriteLine($"Laravel Passport Refresh: Attempt {retry + 1} failed: {ex.Message}");
                    // Continue to next retry
                }
            }
            
            throw new Exception("Laravel Passport: Token refresh failed after all retries");
        }

        public async Task<bool> ValidateTokenAsync(string accessToken)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.GetAsync("/api/user", cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Laravel Passport: Token validation failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Clear the cached token (useful when you need to force a new token request)
        /// </summary>
        public void ClearCachedToken()
        {
            _tokenLock.Wait();
            try
            {
                _cachedAccessToken = null;
                _cachedTokenExpiry = DateTime.MinValue;
                Console.WriteLine("Laravel Passport: Cached token cleared");
            }
            finally
            {
                _tokenLock.Release();
            }
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
            _tokenLock?.Dispose();
        }
    }
} 