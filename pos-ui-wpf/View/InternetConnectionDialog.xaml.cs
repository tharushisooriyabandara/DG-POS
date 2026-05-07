using System;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Timers;
using POS_UI.Services;

namespace POS_UI.View
{
    public partial class InternetConnectionDialog : Window
    {
        // Windows API constants and methods
        private const int GWL_STYLE = -16;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private readonly NetworkConnectivityService _networkService;
        private readonly NavigationStateService _navigationService;
        private readonly Action _onRetrySuccess;
        //private readonly Action _onContinueOffline;
        private bool _isCancelled = false;
        private readonly System.Timers.Timer _fastCheckTimer;

        public InternetConnectionDialog(Action onRetrySuccess = null)
        {
            InitializeComponent();
            
            _networkService = NetworkConnectivityService.Instance;
            _navigationService = new NavigationStateService();
            _onRetrySuccess = onRetrySuccess;

            // Subscribe to connectivity changes
            _networkService.ConnectivityChanged += OnConnectivityChanged;
            
            // Start fast polling timer (checks every 1 second) for immediate detection
            _fastCheckTimer = new System.Timers.Timer(1000); // Check every 1 second
            _fastCheckTimer.Elapsed += async (sender, e) => await FastConnectivityCheckAsync();
            _fastCheckTimer.Start();
            
            // Make the dialog modal but allow system access
            this.ShowInTaskbar = false;
            this.Topmost = false; // Don't make it topmost for all applications
            this.Owner = Application.Current.MainWindow; // Set owner to main window
            
            // Prevent system menu operations (minimize/maximize)
            this.SourceInitialized += (s, e) =>
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                var value = GetWindowLong(hwnd, GWL_STYLE);
                SetWindowLong(hwnd, GWL_STYLE, (int)(value & ~WS_MINIMIZEBOX & ~WS_MAXIMIZEBOX));
            };
            
            // Only bring to front when the main application window is activated
            // This allows users to access system controls like WiFi settings
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Activated += (s, e) => 
                {
                    // Only bring to front if we're still visible and not cancelled
                    if (this.IsVisible && !_isCancelled)
                    {
                        BringToFront();
                    }
                };
            }
        }

        private void OnConnectivityChanged(object sender, bool isConnected)
        {
            if (isConnected)
            {
                // Internet is back, close dialog and restore navigation
                Dispatcher.Invoke(() =>
                {
                    Close();
                    RestoreNavigation();
                });
            }
        }

        private async Task FastConnectivityCheckAsync()
        {
            // Only check if dialog is still visible and not cancelled
            if (!this.IsVisible || _isCancelled)
            {
                return;
            }

            try
            {
                var isConnected = await _networkService.CheckConnectivityAsync();
                
                if (isConnected)
                {
                    // Internet is back, close dialog immediately
                    Dispatcher.Invoke(() =>
                    {
                        if (this.IsVisible && !_isCancelled)
                        {
                            _onRetrySuccess?.Invoke();
                            RestoreNavigation();
                            Close();
                        }
                    });
                }
            }
            catch
            {
                // Ignore errors in fast check, the main timer will handle it
            }
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            RetryButton.IsEnabled = false;
            RetryButton.Content = "Checking...";

            try
            {
                var isConnected = await _networkService.CheckConnectivityAsync();
                
                if (isConnected)
                {
                    // Internet is back
                    _onRetrySuccess?.Invoke();
                    RestoreNavigation();
                    Close();
                }
                else
                {
                    // Still no internet
                    MessageBox.Show("Internet connection is still not available. Please check your network settings.", 
                                  "Connection Failed", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking connection: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
            finally
            {
                RetryButton.IsEnabled = true;
                RetryButton.Content = "Retry";
            }
        }

        /*private void ContinueOfflineButton_Click(object sender, RoutedEventArgs e)
        {
            _onContinueOffline?.Invoke();
            Close();
        }*/

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _isCancelled = true;
            Close();
        }

        private void RestoreNavigation()
        {
            try
            {
                var navigationState = _navigationService.GetNavigationState();
                if (navigationState != null)
                {
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        // Restore the exact page where user left off
                        if (!string.IsNullOrEmpty(navigationState.PageUri))
                        {
                            mainWindow.MainFrame.Navigate(new Uri(navigationState.PageUri, UriKind.Relative));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore navigation: {ex.Message}");
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Allow closing if internet is restored or user clicked cancel
            if (!_networkService.IsConnected && !_isCancelled)
            {
                e.Cancel = true; // Prevent closing
                return;
            }
            
            base.OnClosing(e);
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            // Prevent Alt+F4 from closing the dialog
            if (e.Key == System.Windows.Input.Key.F4 && 
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt)
            {
                e.Handled = true;
                return;
            }
            
            base.OnKeyDown(e);
        }

        private void BringToFront()
        {
            if (this.IsVisible && !this.IsActive && !_isCancelled)
            {
                // Only activate if the main window is active
                if (Application.Current.MainWindow?.IsActive == true)
                {
                    this.Activate();
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Stop fast polling timer
            _fastCheckTimer?.Stop();
            _fastCheckTimer?.Dispose();
            
            // Unsubscribe from connectivity changes
            _networkService.ConnectivityChanged -= OnConnectivityChanged;
            base.OnClosed(e);
        }
    }
} 