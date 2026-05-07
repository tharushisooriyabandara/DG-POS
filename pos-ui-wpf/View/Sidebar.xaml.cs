using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using POS_UI.Services;
using System.Linq;
using System.Windows.Media;

namespace POS_UI
{
    public partial class Sidebar : UserControl, INotifyPropertyChanged
    {
        private Frame _parentFrame;
        private readonly TokenValidationService _tokenValidationService;
        private readonly NavigationStateService _navigationService;
        private DispatcherTimer _clockTimer;
        private DispatcherTimer _speedTimer;
        private string _currentTime;
        private string _networkSpeed = "Checking...";
        private string _shopLogo;
        private readonly MediaPlayer _notificationPlayer = new MediaPlayer();
        private int _lastIncomingOrdersCount = 0;
        private bool _shouldLoopNotificationSound = false;
        public static readonly DependencyProperty IncomingOrdersCountProperty = DependencyProperty.Register(
            nameof(IncomingOrdersCount), typeof(int), typeof(Sidebar), new PropertyMetadata(0));

        public Sidebar()
        {
            InitializeComponent();
            
            this.Loaded += Sidebar_Loaded;
            this.Unloaded += Sidebar_Unloaded;
            _tokenValidationService = new TokenValidationService();
            _navigationService = new NavigationStateService();

            // Initialize clock timer
            InitializeClock();
            InitializeSpeedTimer();

            // Subscribe to global incoming orders count updates
            try
            {
                GlobalDataService.Instance.IncomingOrdersCountChanged += OnIncomingOrdersCountChanged;
                GlobalDataService.Instance.ShopDetailsChanged += OnShopDetailsChanged;
                GlobalDataService.Instance.CurrentUserChanged += OnCurrentUserChanged;
                GlobalDataService.Instance.StopIncomingOrderSoundRequested += OnStopIncomingOrderSoundRequested;
                GlobalDataService.Instance.UseLiveOrdersPageChanged += OnUseLiveOrdersPageChanged;
            }
            catch { /* ignore subscription errors */ }
        }

        private void Sidebar_Loaded(object sender, RoutedEventArgs e)
        {
            _parentFrame = Window.GetWindow(this)?.Content as Frame;
            // Initialize badge from persisted global count on load
            try
            {
                IncomingOrdersCount = GlobalDataService.Instance.CurrentIncomingOrdersCount;
                _lastIncomingOrdersCount = IncomingOrdersCount;
                ShopLogo = GlobalDataService.Instance.ShopDetails?.ShopLogo;
                UpdateRoleFlags(GlobalDataService.Instance.CurrentUser);
            }
            catch { }

            // Start the clock timer
            StartClock();
            StartSpeedTimer();
        }

        private bool _isOutletAdmin;
        public bool IsOutletAdmin
        {
            get => _isOutletAdmin;
            private set
            {
                if (_isOutletAdmin != value)
                {
                    _isOutletAdmin = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>When true, show Live Orders button in sidebar; when false (Orders Page selected in Configure Orders), hide it.</summary>
        public bool ShowLiveOrdersButton => GlobalDataService.Instance.UseLiveOrdersPage;
        /// <summary>When true, show Orders button in sidebar; when false (Live Orders Page selected in Configure Orders), hide it.</summary>
        public bool ShowOrdersButton => !GlobalDataService.Instance.UseLiveOrdersPage;

        private void OnCurrentUserChanged(POS_UI.Models.CurrentUserModel user)
        {
            Dispatcher.Invoke(() => UpdateRoleFlags(user));
        }

        private void UpdateRoleFlags(POS_UI.Models.CurrentUserModel user)
        {
            var role = (user?.Role ?? string.Empty).Trim();
            //var roleId = (user?.RoleId ?? string.Empty).Trim();
            string[] outletAdminAliases = new[] { "OutletAdmin", "Outlet Admin", "OUTLETADMIN", "OUTLET_ADMIN" };
            bool isMatch(string s) => outletAdminAliases.Any(a => string.Equals(s, a, StringComparison.OrdinalIgnoreCase));
            IsOutletAdmin = isMatch(role);
        }

        public int IncomingOrdersCount
        {
            get => (int)GetValue(IncomingOrdersCountProperty);
            private set => SetValue(IncomingOrdersCountProperty, value);
        }

        public string CurrentTime
        {
            get => _currentTime;
            private set
            {
                if (_currentTime != value)
                {
                    _currentTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public string NetworkSpeed
        {
            get => _networkSpeed;
            private set
            {
                if (_networkSpeed != value)
                {
                    _networkSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ShopLogo
        {
            get => _shopLogo;
            private set
            {
                if (_shopLogo != value)
                {
                    _shopLogo = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnIncomingOrdersCountChanged(int newCount)
        {
            // Ensure UI thread update
            System.Diagnostics.Debug.WriteLine($"Sidebar: OnIncomingOrdersCountChanged called with count {newCount}");
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"Sidebar: Setting IncomingOrdersCount to {newCount}");
                IncomingOrdersCount = newCount;

                // Start notification sound only when count increases, user is not on Cashier,
                // and a loop is not already active
                try
                {
                    bool hasIncreased = newCount > _lastIncomingOrdersCount;
                    if (hasIncreased && !IsOnCashierPage() && !_shouldLoopNotificationSound)
                    {
                        PlayNotificationSound();
                    }

                    // Stop looping sound if count becomes zero or less
                    if (newCount <= 0)
                    {
                        try { _shouldLoopNotificationSound = false; _notificationPlayer.Stop(); } catch { }
                    }
                }
                catch { /* ignore sound errors */ }

                _lastIncomingOrdersCount = newCount;
                
            });
        }

        private void OnShopDetailsChanged(POS_UI.Models.ShopModel shop)
        {
            Dispatcher.Invoke(() =>
            {
                ShopLogo = shop?.ShopLogo;
            });
        }

        private void OnUseLiveOrdersPageChanged()
        {
            Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(ShowLiveOrdersButton));
                OnPropertyChanged(nameof(ShowOrdersButton));
            });
        }

        private void CashierButton_Click(object sender, RoutedEventArgs e)
        {
            _navigationService.SaveNavigationState("/View/CashierHomePage.xaml", "CashierHomePage");
            _parentFrame?.Navigate(new Uri("/View/CashierHomePage.xaml", UriKind.Relative));
        }

        private void Sidebar_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                GlobalDataService.Instance.IncomingOrdersCountChanged -= OnIncomingOrdersCountChanged;
                GlobalDataService.Instance.ShopDetailsChanged -= OnShopDetailsChanged;
                GlobalDataService.Instance.UseLiveOrdersPageChanged -= OnUseLiveOrdersPageChanged;
                //GlobalDataService.Instance.StopIncomingOrderSoundRequested -= OnStopIncomingOrderSoundRequested;
            }
            catch { /* ignore unsubscribe errors */ }

            // Stop the clock timer
            StopClock();
            StopSpeedTimer();
        }

        private void TablesButton_Click(object sender, RoutedEventArgs e)
        {
            TryNotifyCashierNavigatingAway();
            _navigationService.SaveNavigationState("/View/TablesPage.xaml", "TablesPage");
            _parentFrame?.Navigate(new Uri("/View/TablesPage.xaml", UriKind.Relative));
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Before logging out, persist ongoing order info (if any) into order config
                await OngoingOrderConfigPersistence.TrySaveFromCartAsync();

                // Call logout API
                var apiService = new ApiService();
                var logoutSuccess = await apiService.LogoutAsync();
                
                if (!logoutSuccess)
                {
                    Console.WriteLine("Logout API call failed, but continuing with local logout");
                }
                
                // Clear all tokens
                _tokenValidationService.ClearTokens();
                
                // Clear global data (current user and shop details)
                var globalDataService = GlobalDataService.Instance;
                globalDataService.ClearData();
                
                //Clear draft Orders
                var draftStorageService = new DraftStorageService();
                draftStorageService.ClearAllDrafts();
                
                // Clear navigation state on logout
                _navigationService.ClearNavigationState();
                
                // Navigate to login page
                if (_parentFrame != null)
                {
                    _parentFrame.Navigate(new Uri("/View/LoginPage.xaml", UriKind.Relative));
                }
                else
                {
                    MessageBox.Show("Navigation service is not available", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during logout: {ex.Message}", "Logout Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void KitchenButton_Click(object sender, RoutedEventArgs e)
        {
            TryNotifyCashierNavigatingAway();
            var dc = DataContext;
            var prop = dc?.GetType().GetProperty("CurrentPage");
            if (prop != null && prop.CanWrite)
                prop.SetValue(dc, "Kitchen");
            _navigationService.SaveNavigationState("/View/KitchenPage.xaml", "KitchenPage");
            _parentFrame?.Navigate(new Uri("/View/KitchenPage.xaml", UriKind.Relative));
        }

        private void LiveOrdersButton_Click(object sender, RoutedEventArgs e)
        {
            TryNotifyCashierNavigatingAway();
            var dc = DataContext;
            var prop = dc?.GetType().GetProperty("CurrentPage");
            if (prop != null && prop.CanWrite)
                prop.SetValue(dc, "LiveOrders");
            _navigationService.SaveNavigationState("/View/LiveOrdersPage.xaml", "LiveOrdersPage");
            _parentFrame?.Navigate(new Uri("/View/LiveOrdersPage.xaml", UriKind.Relative));
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            TryNotifyCashierNavigatingAway();
            var dc = DataContext;
            var prop = dc?.GetType().GetProperty("CurrentPage");
            if (prop != null && prop.CanWrite)
                prop.SetValue(dc, "Settings");
            _navigationService.SaveNavigationState("/View/SettingsPage.xaml", "SettingsPage");
            _parentFrame?.Navigate(new Uri("/View/SettingsPage.xaml", UriKind.Relative));
        }

        private void InventoryButton_Click(object sender, RoutedEventArgs e)
        {
            TryNotifyCashierNavigatingAway();
            var dc = DataContext;
            var prop = dc?.GetType().GetProperty("CurrentPage");
            if (prop != null && prop.CanWrite)
                prop.SetValue(dc, "Inventory");
            _navigationService.SaveNavigationState("/View/InventoryPage.xaml", "InventoryPage");
            _parentFrame?.Navigate(new Uri("/View/InventoryPage.xaml", UriKind.Relative));
        }

        private void CustomerButton_Click(object sender, RoutedEventArgs e)
        {
            TryNotifyCashierNavigatingAway();
            var dc = DataContext;
            var prop = dc?.GetType().GetProperty("CurrentPage");
            if (prop != null && prop.CanWrite)
                prop.SetValue(dc, "Customer");
            _navigationService.SaveNavigationState("/View/CustomerPage.xaml", "CustomerPage");
            _parentFrame?.Navigate(new Uri("/View/CustomerPage.xaml", UriKind.Relative));
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            TryNotifyCashierNavigatingAway();
            var dc = DataContext;
            var prop = dc?.GetType().GetProperty("CurrentPage");
            if (prop != null && prop.CanWrite)
                prop.SetValue(dc, "History");
            _navigationService.SaveNavigationState("/View/HistoryPage.xaml", "HistoryPage");
            _parentFrame?.Navigate(new Uri("/View/HistoryPage.xaml", UriKind.Relative));
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            TryNotifyCashierNavigatingAway();
            var dc = DataContext;
            var prop = dc?.GetType().GetProperty("CurrentPage");
            if (prop != null && prop.CanWrite)
                prop.SetValue(dc, "Reports");
            _navigationService.SaveNavigationState("/View/ReportsPage.xaml", "ReportsPage");
            _parentFrame?.Navigate(new Uri("/View/ReportsPage.xaml", UriKind.Relative));
        }

        private void TryNotifyCashierNavigatingAway()
        {
            try
            {
                // If current page is Cashier, invoke VM hook before leaving
                if (_parentFrame?.Content is CashierHomePage cashierPage && cashierPage.DataContext is ViewModels.CashierHomeViewModel vm)
                {
                    vm.HandleNavigatingAwayFromCashier();
                }
            }
            catch { /* ignore */ }
        }

        private void InitializeClock()
        {
            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += ClockTimer_Tick;
            
            // Set initial time
            UpdateTime();
        }

        private void StartClock()
        {
            if (_clockTimer != null && !_clockTimer.IsEnabled)
            {
                _clockTimer.Start();
            }
        }

        private void StopClock()
        {
            if (_clockTimer != null && _clockTimer.IsEnabled)
            {
                _clockTimer.Stop();
            }
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            UpdateTime();
        }

        private void UpdateTime()
        {
            CurrentTime = DateTime.Now.ToString("hh:mm tt");
        }

        private void InitializeSpeedTimer()
        {
            _speedTimer = new DispatcherTimer();
            _speedTimer.Interval = TimeSpan.FromSeconds(30);
            _speedTimer.Tick += async (s, e) => await MeasureSpeedAsync();
        }

        private void StartSpeedTimer()
        {
            if (_speedTimer != null && !_speedTimer.IsEnabled)
            {
                _speedTimer.Start();
                // Initial check
                _ = MeasureSpeedAsync();
            }
        }

        private void StopSpeedTimer()
        {
            if (_speedTimer != null && _speedTimer.IsEnabled)
            {
                _speedTimer.Stop();
            }
        }

        private async System.Threading.Tasks.Task MeasureSpeedAsync()
        {
            try
            {
                var api = new ApiService();
                var latency = await api.MeasureNetworkLatencyAsync();
                NetworkSpeed = $"Ping: {latency}";
            }
            catch
            {
                NetworkSpeed = "Offline";
            }
        }

        private void OnStopIncomingOrderSoundRequested()
        {
            try
            {
                _shouldLoopNotificationSound = false;
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _notificationPlayer.MediaEnded -= NotificationPlayer_MediaEnded;
                        _notificationPlayer.Stop();
                    }
                    catch { }
                });
            }
            catch { }
        }

        private bool IsOnCashierPage()
        {
            try
            {
                if (_parentFrame?.Content is CashierHomePage) return true;
                return false;
            }
            catch { return false; }
        }

        private void PlayNotificationSound()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var soundPath = System.IO.Path.Combine(baseDir, "Source", "sound.mp3");
                if (!System.IO.File.Exists(soundPath)) return;

                // Reopen to ensure fresh play even if already opened
                _shouldLoopNotificationSound = true;
                _notificationPlayer.Open(new Uri(soundPath, UriKind.Absolute));
                _notificationPlayer.Position = TimeSpan.Zero;
                _notificationPlayer.Play();

                _notificationPlayer.MediaEnded -= NotificationPlayer_MediaEnded;
                _notificationPlayer.MediaEnded += NotificationPlayer_MediaEnded;
            }
            catch { /* ignore */ }
        }
        
        private void NotificationPlayer_MediaEnded(object sender, EventArgs e)
        {
            try
            {
                if (_shouldLoopNotificationSound)
                {
                    _notificationPlayer.Position = TimeSpan.Zero;
                    _notificationPlayer.Play();
                }
            }
            catch { }
        }
        
    }
} 