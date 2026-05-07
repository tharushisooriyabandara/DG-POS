using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using POS_UI.Models;
using System.Windows.Threading;
using POS_UI.Services;
using System.Security.Claims;
using POS_UI.Properties;
using System.Linq;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace POS_UI.ViewModels
{
    public class LoginViewModel : LoadingViewModelBase
    {
        private string _selectedUserType;
        private ObservableCollection<PinBoxViewModel> _pinBoxes;
        private ICommand _loginCommand;
        private ICommand _keypadCommand;
        private NavigationService _navigationService;
        private string _pin;
        private ICommand _deleteLastDigitCommand;
        private string _errorMessage;
        private bool _hasError;
        private ICommand _clearErrorCommand;
        private readonly TokenService _tokenService;
        private TokenModel _currentTokens;
        private ApiService _apiService;
        private UserModel _selectedUser;
        private bool _isLoggingIn;

        public string SelectedUserType
        {
            get => _selectedUserType;
            set
            {
                if (_selectedUserType != value)
                {
                    _selectedUserType = value;
                    OnPropertyChanged();
                    UpdatePinBoxCount();
                }
            }
        }

        public ObservableCollection<PinBoxViewModel> PinBoxes
        {
            get => _pinBoxes;
            set
            {
                _pinBoxes = value;
                OnPropertyChanged();
            }
        }

        public string Pin
        {
            get => _pin;
            set { _pin = value; OnPropertyChanged(); }
        }

        public ObservableCollection<UserModel> Users { get; set; }

        public UserModel SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (_selectedUser != value)
                {
                    _selectedUser = value;
                    OnPropertyChanged();
                    if (_selectedUser != null)
                    {
                        SelectedUserType = _selectedUser.Role;
                        UpdatePinBoxCount();
                    }
                }
            }
        }

        public ICommand LoginCommand => _loginCommand ??= new RelayCommand(ExecuteLogin);
        public ICommand KeypadCommand => _keypadCommand ??= new RelayCommand<string>(ExecuteKeypad);
        public ICommand DeleteLastDigitCommand => _deleteLastDigitCommand ??= new RelayCommand(DeleteLastDigit);
        public ICommand ClearErrorCommand => _clearErrorCommand ??= new RelayCommand(ClearError);


        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool HasError
        {
            get => _hasError;
            set { _hasError = value; OnPropertyChanged(); }
        }

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set { _isLoggingIn = value; OnPropertyChanged(); }
        }

        /// <summary>Shown on the login screen when there are CREATED orders (e.g. after idle logout while Firebase still updates).</summary>
        private int _incomingOrdersBadgeCount;
        public int IncomingOrdersBadgeCount
        {
            get => _incomingOrdersBadgeCount;
            private set
            {
                if (_incomingOrdersBadgeCount == value) return;
                _incomingOrdersBadgeCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasIncomingOrdersBadge));
            }
        }

        public bool HasIncomingOrdersBadge => _incomingOrdersBadgeCount > 0;

        private bool _incomingOrdersListenerAttached;
        private readonly MediaPlayer _incomingOrderNotificationPlayer = new MediaPlayer();
        private bool _shouldLoopIncomingOrderNotificationSound;

        public void AttachIncomingOrdersListener()
        {
            if (_incomingOrdersListenerAttached) return;
            _incomingOrdersListenerAttached = true;
            var g = GlobalDataService.Instance;
            IncomingOrdersBadgeCount = g.CurrentIncomingOrdersCount;
            g.IncomingOrdersCountChanged += OnGlobalIncomingOrdersCountChanged;
            try { g.StopIncomingOrderSoundRequested += OnGlobalStopIncomingOrderSoundRequested; } catch { }
        }

        public void DetachIncomingOrdersListener()
        {
            if (!_incomingOrdersListenerAttached) return;
            _incomingOrdersListenerAttached = false;
            try { GlobalDataService.Instance.IncomingOrdersCountChanged -= OnGlobalIncomingOrdersCountChanged; } catch { }
            try { GlobalDataService.Instance.StopIncomingOrderSoundRequested -= OnGlobalStopIncomingOrderSoundRequested; } catch { }
            StopIncomingOrderAlertSound();
        }

        /// <summary>Stops the looping new-order sound (e.g. when the user taps the login alert card).</summary>
        public void StopIncomingOrderAlertSound()
        {
            try
            {
                _shouldLoopIncomingOrderNotificationSound = false;
                void StopCore()
                {
                    try
                    {
                        _incomingOrderNotificationPlayer.MediaEnded -= IncomingOrderNotificationPlayer_MediaEnded;
                        _incomingOrderNotificationPlayer.Stop();
                    }
                    catch { /* ignore */ }
                }
                if (Application.Current?.Dispatcher?.CheckAccess() == true)
                    StopCore();
                else
                    Application.Current?.Dispatcher?.Invoke(StopCore);
            }
            catch { /* ignore */ }
        }

        private void OnGlobalStopIncomingOrderSoundRequested()
        {
            StopIncomingOrderAlertSound();
        }

        private void OnGlobalIncomingOrdersCountChanged(int newCount)
        {
            try
            {
                void Apply()
                {
                    var prevCount = _incomingOrdersBadgeCount;
                    IncomingOrdersBadgeCount = newCount;

                    try
                    {
                        if (newCount > prevCount && newCount > 0 && !_shouldLoopIncomingOrderNotificationSound)
                            PlayIncomingOrderNotificationSound();
                        if (newCount <= 0)
                            StopIncomingOrderAlertSound();
                    }
                    catch { /* ignore sound errors */ }
                }
                if (Application.Current?.Dispatcher?.CheckAccess() == true)
                    Apply();
                else
                    Application.Current?.Dispatcher?.Invoke(Apply);
            }
            catch { /* ignore */ }
        }

        private void PlayIncomingOrderNotificationSound()
        {
            try
            {
                var soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Source", "sound.mp3");
                if (!File.Exists(soundPath)) return;

                _shouldLoopIncomingOrderNotificationSound = true;
                _incomingOrderNotificationPlayer.Open(new Uri(soundPath, UriKind.Absolute));
                _incomingOrderNotificationPlayer.Position = TimeSpan.Zero;
                _incomingOrderNotificationPlayer.Play();

                _incomingOrderNotificationPlayer.MediaEnded -= IncomingOrderNotificationPlayer_MediaEnded;
                _incomingOrderNotificationPlayer.MediaEnded += IncomingOrderNotificationPlayer_MediaEnded;
            }
            catch { /* ignore */ }
        }

        private void IncomingOrderNotificationPlayer_MediaEnded(object sender, EventArgs e)
        {
            try
            {
                if (_shouldLoopIncomingOrderNotificationSound)
                {
                    _incomingOrderNotificationPlayer.Position = TimeSpan.Zero;
                    _incomingOrderNotificationPlayer.Play();
                }
            }
            catch { /* ignore */ }
        }

        public event Action LoginSucceeded;

        public LoginViewModel()
        {
            PinBoxes = new ObservableCollection<PinBoxViewModel>();
            //SelectedUserType = "Cashier"; // Default selection
            UpdatePinBoxCount();
            _tokenService = new TokenService();
            Users = new ObservableCollection<UserModel>();
            
            // Initialize API service with settings
            _apiService = new ApiService();
            FetchUsersFromApi();

            //Listen for network connectivity changes
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

            //Check initial network connectivity
            CheckInitialNetworkConnectivity();
        }
        private void CheckInitialNetworkConnectivity()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                // Delay showing the dialog until the main window is ready
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Wait for the main window to be fully loaded
                    if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded)
                    {
                        ShowInternetConnectionDialog();
                    }
                    else
                    {
                        // If main window isn't ready, wait a bit and try again
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ShowInternetConnectionDialog();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void ShowInternetConnectionDialog()
        {
            try
            {
                var dialog = new POS_UI.View.InternetConnectionDialog();
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                // Fallback to MessageBox if dialog fails
                MessageBox.Show("No internet connection detected. Please check your network connection and try again.", 
                              "Network Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Warning);
                Console.WriteLine($"Failed to show InternetConnectionDialog: {ex.Message}");
            }
        }

        private async void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            if (e.IsAvailable)
            {
                //Console.WriteLine("Network is available, fetching users from API");
                _apiService = new ApiService();
                
                // Set bearer token if available
                var accessToken = Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    _apiService.SetBearerToken(accessToken);
                }
                
                FetchUsersFromApi();
            }
        }
        public void SetNavigationService(NavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        public void RefreshApiService()
        {
            _apiService = new ApiService();
            FetchUsersFromApi();
        }

        private void UpdatePinBoxCount()
        {
            PinBoxes.Clear();
            int count = 4;
            if (SelectedUser != null)
            {
                if (SelectedUser.Role == "Outlet Admin")
                    count = 6;
                else if (SelectedUser.Role == "Cashier")
                    count = 4;
            }
            else if (SelectedUserType == "Admin" || SelectedUserType == "Outlet Admin")
            {
                count = 6;
            }
            for (int i = 0; i < count; i++)
            {
                var pinBox = new PinBoxViewModel();
                pinBox.PropertyChanged += PinBox_PropertyChanged;
                PinBoxes.Add(pinBox);
            }
            OnPropertyChanged(nameof(PinBoxes));
        }

        private void PinBox_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Text")
            {
                if (PinBoxes.All(box => !string.IsNullOrEmpty(box.Text)))
                {
                    ExecuteLogin();
                }
            }
        }

        private async void ExecuteLogin()
        {
            string enteredPin = string.Join("", PinBoxes.Select(p => p.Text));
            var user = SelectedUser;
            if (user == null)
            {
                HasError = true;
                ErrorMessage = "Error: Please select a user";
                return;
            }
            IsLoggingIn = true;
            try
            {
                // First, authenticate with the Go API
                var result = await _apiService.LoginAsync(user.Email, enteredPin);
                Properties.Settings.Default.AccessToken = result.accessToken;
                Properties.Settings.Default.RefreshToken = result.refreshToken;
                Properties.Settings.Default.AccessTokenExpiry = result.accessTokenExpiry;
                Properties.Settings.Default.RefreshTokenExpiry = result.refreshTokenExpiry;
                Properties.Settings.Default.Save();

                // Print the login API response to the console/terminal
                Console.WriteLine("Go API Login Response: SUCCESS");
                Console.WriteLine($"AccessToken: {result.accessToken}");
                Console.WriteLine($"RefreshToken: {result.refreshToken}");
                Console.WriteLine($"Token Expired: {_tokenService.IsTokenExpired(result.accessToken)}");

                _apiService.SetBearerToken(result.accessToken);

                // After successful Go login, authenticate with Laravel Passport
                try
                {
                    var laravelPassportService = new LaravelPassportService();
                    
                    // Call Laravel authentication directly with header
                    Console.WriteLine("Calling Laravel Passport authentication...");
                    var laravelBearerToken = await laravelPassportService.GetAccessTokenAsync();
                    
                    // Store the Laravel Passport bearer token
                    Properties.Settings.Default.LaravelBearerToken = laravelBearerToken;
                    Properties.Settings.Default.Save();
                    
                    Console.WriteLine("Laravel Passport Authentication: SUCCESS");
                    Console.WriteLine($"Laravel Bearer Token: {laravelBearerToken}");
                }
                catch (Exception laravelEx)
                {
                    Console.WriteLine("Laravel Passport Authentication: FAILED");
                    Console.WriteLine($"Laravel Error: {laravelEx.Message}");
                    
                    // Don't fail the entire login if Laravel authentication fails
                    // The user can still use the Go API functionality
                }

                // Load current user and shop details after successful login
                var globalDataService = GlobalDataService.Instance;
                var dataLoaded = await globalDataService.LoadDataAfterLoginAsync();
                
                if (!dataLoaded)
                {
                    // If shop missing menu or delivery platform, show message and do not log in
                    var shop = GlobalDataService.Instance.ShopDetails;
                    if (shop == null || shop.DeliveryPlatform == null || shop.DeliveryPlatform.SelectedMenu <= 0)
                    {
                        HasError = true;
                        ErrorMessage = "Please publish a menu for this outlet";
                        // Clear tokens so user stays logged out
                        Properties.Settings.Default.AccessToken = string.Empty;
                        Properties.Settings.Default.RefreshToken = string.Empty;
                        Properties.Settings.Default.LaravelBearerToken = string.Empty;
                        Properties.Settings.Default.Save();
                        return;
                    }

                    Console.WriteLine("Warning: Failed to load user and shop details after login");
                    HasError = true;
                    ErrorMessage = "Login failed. Please try again.";
                    return;
                }

                HasError = false;
                ErrorMessage = "";
                // Raise event instead of navigating here
                LoginSucceeded?.Invoke();
            }
            catch (Exception ex)
            {
                HasError = true;
                // Check if it's a PIN-related error and provide a more user-friendly message
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized") || 
                    ex.Message.Contains("invalid") || ex.Message.Contains("incorrect") ||
                    ex.Message.Contains("wrong") || ex.Message.Contains("failed"))
                {
                    ErrorMessage = "Error: Please enter the correct PIN code";
                }
                else
                {
                    ErrorMessage = "Error: Login failed. Please try again.";
                }
                Console.WriteLine("Go API Login Response: FAILED");
                Console.WriteLine($"Error: {ex.Message}");
                //MessageBox.Show($"Login failed: {ex.Message}", "Backend Error");
                ClearPinBoxes();
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        private void TestTokenStorage()
        {
            try
            {
                // Verify tokens were stored correctly
                var storedAccessToken = Properties.Settings.Default.AccessToken;
                var storedRefreshToken = Properties.Settings.Default.RefreshToken;
                var storedAccessTokenExpiry = Properties.Settings.Default.AccessTokenExpiry;
                var storedRefreshTokenExpiry = Properties.Settings.Default.RefreshTokenExpiry;

                // Get time until expiration
                var timeUntilExpiry = _tokenService.GetTimeUntilExpiry(storedAccessToken);
                var isExpired = _tokenService.IsTokenExpired(storedAccessToken);

                // Create a message with token information
                var message = $"Token Storage Test Results:\n\n" +
                            $"Access Token: {(string.IsNullOrEmpty(storedAccessToken) ? "Not Stored" : "Stored Successfully")}\n" +
                            $"Refresh Token: {(string.IsNullOrEmpty(storedRefreshToken) ? "Not Stored" : "Stored Successfully")}\n" +
                            $"Access Token Expiry: {storedAccessTokenExpiry}\n" +
                            $"Refresh Token Expiry: {storedRefreshTokenExpiry}\n\n" +
                            $"Time Until Expiry: {timeUntilExpiry.TotalMinutes:F2} minutes\n" +
                            $"Is Token Expired: {isExpired}\n";

                MessageBox.Show(message, "Token Test Results", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error testing token storage: {ex.Message}", "Token Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteKeypad(string digit)
        {
            if (int.TryParse(digit, out _))
            {
                // Clear error message when user starts entering a new PIN
                if (HasError)
                {
                    ClearError();
                }
                
                var emptyBox = PinBoxes.FirstOrDefault(box => string.IsNullOrEmpty(box.Text));
                if (emptyBox != null)
                {
                    emptyBox.Text = digit;
                }
            }
        }

        private void ClearPinBoxes()
        {
            foreach (var box in PinBoxes)
            {
                box.Text = string.Empty;
            }
        }

        private void DeleteLastDigit()
        {
            for (int i = PinBoxes.Count - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(PinBoxes[i].Text))
                {
                    PinBoxes[i].Text = string.Empty;
                    break;
                }
            }
        }

        private void ClearError()
        {
            ErrorMessage = "";
            HasError = false;
        }

        private async void FetchUsersFromApi()
        {
            await SetLoadingAsync(async () =>
            {
                try
                {
                    var usersFromApi = await _apiService.GetUsersAsync();
                    
                    // Sort users by FullName in ascending order
                    var sortedUsers = usersFromApi.OrderBy(user => user.FullName).ToList();
                    
                    // Use Dispatcher to update UI collection on the main thread
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Users.Clear();
                        
                        foreach (var user in sortedUsers)
                        {
                            Users.Add(user);
                        }
                        
                        // Set the first user as default if available
                        if (Users.Count > 0)
                        {
                            SelectedUser = Users[0];
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to fetch users: {ex.Message}");
                    try { POS_UI.Services.LogService.Error("LoginViewModel: Failed to fetch users", ex); } catch { }
                }
            });
        }




    }

} 