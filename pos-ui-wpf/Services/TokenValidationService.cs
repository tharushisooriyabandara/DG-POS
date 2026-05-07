using System;
using System.Threading.Tasks;
using POS_UI.Models;
using POS_UI.Properties;
using System.Security.Claims;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Windows;
using POS_UI.View;

namespace POS_UI.Services
{
    public class TokenValidationService
    {
        private static bool _isLoggingOut;
        private readonly TokenService _tokenService;

        public TokenValidationService()
        {
            _tokenService = new TokenService();
        }

        public void LogoutAndNavigateToLogin(string reason = null)
        {
            if (_isLoggingOut) return;
            _isLoggingOut = true;
            try
            {
                // Persist cart as ongoing_order when temp payments exist (idle logout, token expiry, etc.) while tokens still work.
                // Run off the UI thread: blocking GetResult() on the dispatcher can deadlock with HTTP continuations.
                try
                {
                    Task.Run(() => OngoingOrderConfigPersistence.TrySaveFromCartAsync().GetAwaiter().GetResult())
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TokenValidation] Ongoing order persist before logout: {ex.Message}");
                }

                // Clear tokens and local state
                try { ClearTokens(); } catch { }
                try { new LocalStorageService().ClearAllData(); } catch { }
                try { new NavigationStateService().ClearNavigationState(); } catch { }

                // Navigate back to LoginPage on UI thread
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (System.Windows.Application.Current.MainWindow is MainWindow main)
                        {
                            main.MainFrame?.Navigate(new LoginPage());
                        }
                    }
                    catch { }
                });
            }
            finally
            {
                _isLoggingOut = false;
            }
        }

        public async Task<bool> IsTokenValidAsync()
        {
            try
            {
               // MessageBox.Show("IsTokenValid starting...");
                
                var accessToken = Properties.Settings.Default.AccessToken;
                var refreshToken = Properties.Settings.Default.RefreshToken;
               // MessageBox.Show($"AccessToken exists: {!string.IsNullOrEmpty(accessToken)}");
                //Console.WriteLine($"AccessToken exists: {!string.IsNullOrEmpty(accessToken)}");
                //MessageBox.Show($"RefreshToken exists: {!string.IsNullOrEmpty(refreshToken)}");
                //Console.WriteLine($"RefreshToken exists: {!string.IsNullOrEmpty(refreshToken)}");
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    LogoutAndNavigateToLogin("No access token");
                    return false;
                }

                // Only check if token is expired
                //MessageBox.Show("Checking if token is expired...");
                //Console.WriteLine("Checking if token is expired...");
                if (_tokenService.IsTokenExpired(accessToken))
                {
                   // MessageBox.Show("Access token is expired, checking refresh token...");
                    //Console.WriteLine("Access token is expired, checking refresh token...");
                    // If access token is expired, try to refresh
                    if (!string.IsNullOrEmpty(refreshToken) && !_tokenService.IsTokenExpired(refreshToken))
                    {
                       // MessageBox.Show("Refresh token is valid, attempting to refresh...");
                        //Console.WriteLine("Refresh token is valid, attempting to refresh...");
                        try
                        {
                            var apiService = new ApiService();
                           // Console.WriteLine("Created ApiService instance");
                            
                            // Use a shorter timeout and async pattern to prevent hanging
                            var refreshTask = apiService.RefreshTokenAsync(refreshToken);
                            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5)); // 5 minute timeout
                            
                            var completedTask = await Task.WhenAny(refreshTask, timeoutTask);
                            if (completedTask == timeoutTask)
                            {
                                LogoutAndNavigateToLogin("Refresh token timeout");
                                return false;
                            }
                            
                            //Console.WriteLine("RefreshTokenAsync task completed");
                            var (newAccessToken, newRefreshToken, newAccessTokenExpiry, newRefreshTokenExpiry) = refreshTask.Result;
                            //Console.WriteLine("Got refresh result");
                            Properties.Settings.Default.AccessToken = newAccessToken;
                            Properties.Settings.Default.RefreshToken = newRefreshToken;
                            Properties.Settings.Default.AccessTokenExpiry = newAccessTokenExpiry;
                            Properties.Settings.Default.RefreshTokenExpiry = newRefreshTokenExpiry;
                            Properties.Settings.Default.Save();
                            //MessageBox.Show("Token refresh successful, returning true");
                            //Console.WriteLine("Token refresh successful, returning true");
                            return true;
                        }
                        catch (Exception refreshEx)
                        {
                            LogoutAndNavigateToLogin("Refresh token failed");
                            return false;
                        }
                    }
                    else
                    {
                        LogoutAndNavigateToLogin("Refresh token invalid or expired");
                        return false;
                    }
                }
                //MessageBox.Show("Access token is valid, returning true");
                //Console.WriteLine("Access token is valid, returning true");
                return true;
            }
            catch (Exception ex)
            {
                //Console.WriteLine("IsTokenValid error: " + ex.Message);
                //Console.WriteLine("Stack trace: " + ex.StackTrace);
                //MessageBox.Show("IsTokenValid error: " + ex.Message + "\n\nStackTrace: " + ex.StackTrace);
                return false;
            }
        }

        // Keep the synchronous version for backward compatibility
        public bool IsTokenValid()
        {
            try
            {
                // For now, just check if token exists and is not expired without refresh
                var accessToken = Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    return false;
                }
                return !_tokenService.IsTokenExpired(accessToken);
            }
            catch
            {
                return false;
            }
        }

        public bool IsRefreshTokenValid()
        {
            try
            {
                var refreshToken = Properties.Settings.Default.RefreshToken;
                var refreshTokenExpiry = Properties.Settings.Default.RefreshTokenExpiry;

                if (string.IsNullOrEmpty(refreshToken))
                    return false;

                return DateTime.UtcNow < refreshTokenExpiry;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RefreshTokenAsync(UserModel user)
        {
            // Token refresh should be handled by the backend. This method should call the backend to refresh tokens.
            // Remove any frontend token generation logic.
            return false;
        }

        public ClaimsPrincipal GetCurrentUser()
        {
            var accessToken = Properties.Settings.Default.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
                return null;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(accessToken);
                var identity = new ClaimsIdentity(jwtToken.Claims, "jwt");
                return new ClaimsPrincipal(identity);
            }
            catch
            {
            return null;
            }
        }

        public void ClearTokens()
        {
            try
            {
                Properties.Settings.Default.AccessToken = string.Empty;
                Properties.Settings.Default.RefreshToken = string.Empty;
                Properties.Settings.Default.AccessTokenExpiry = DateTime.MinValue;
                Properties.Settings.Default.RefreshTokenExpiry = DateTime.MinValue;
                Properties.Settings.Default.LaravelBearerToken = string.Empty;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to clear tokens: " + ex.Message);
            }
        }
    }
} 