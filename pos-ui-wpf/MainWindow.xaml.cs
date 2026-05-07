using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using POS_UI.Services;
using POS_UI.View;
using System.Security.Claims;

namespace POS_UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly TokenValidationService _tokenValidationService;
        private readonly NetworkConnectivityService _networkService;
        private readonly NavigationStateService _navigationService;
        private InternetConnectionDialog _internetDialog;

        public MainWindow()
        {
            try
            {
                //System.Windows.MessageBox.Show("MainWindow constructor starting...");
                InitializeComponent();
                // Set window/taskbar icon from remote PNG
                try
                {
                    var iconUri = new Uri("https://delivergate-logos.s3.eu-west-2.amazonaws.com/POSPNG.png", UriKind.Absolute);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = iconUri;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.EndInit();
                    this.Icon = bitmap;
                }
                catch { /* non-fatal if icon fails to load */ }
                //System.Windows.MessageBox.Show("InitializeComponent completed");
                _tokenValidationService = new TokenValidationService();
                _networkService = NetworkConnectivityService.Instance;
                _navigationService = new NavigationStateService();
                
                // Subscribe to network connectivity changes
                _networkService.ConnectivityChanged += OnConnectivityChanged;
                
                // Start network monitoring
                _networkService.StartMonitoring();
                
                //System.Windows.MessageBox.Show("TokenValidationService created");
                RestoreLastPage();
                //System.Windows.MessageBox.Show("RestoreLastPage completed");
            }
            catch (Exception ex)
            {
                //System.Windows.MessageBox.Show("MainWindow constructor error: " + ex.Message + "\n\nStackTrace: " + ex.StackTrace);
                throw; // Re-throw to be caught by the App.xaml.cs
            }
        }

        private void RestoreLastPage()
        {
            try
            {
                //System.Windows.MessageBox.Show("RestoreLastPage starting...");
                var tokenService = new TokenValidationService();
                //System.Windows.MessageBox.Show("TokenService created in RestoreLastPage");
                
                if (tokenService.IsTokenValid())
                {
                    // Check if we have a saved navigation state
                    var navigationState = _navigationService.GetNavigationState();
                    if (navigationState != null)
                    {
                        // Restore the exact page where user left off
                        MainFrame.Navigate(new Uri(navigationState.PageUri, UriKind.Relative));
                    }
                    else
                    {
                        // Default to CashierHomePage
                        MainFrame.Navigate(new Uri("/View/CashierHomePage.xaml", UriKind.Relative));
                    }
                   // System.Windows.MessageBox.Show("Navigation to CashierHomePage completed");
                }
                else
                {
                    //System.Windows.MessageBox.Show("Token is invalid, navigating to LoginPage...");
                    // Tokens invalid/expired, prompt login
                    MainFrame.Navigate(new LoginPage());
                    //System.Windows.MessageBox.Show("Navigation to LoginPage completed");
                }
            }
            catch (Exception ex)
            {
               // System.Windows.MessageBox.Show("RestoreLastPage error: " + ex.Message + "\n\nStackTrace: " + ex.StackTrace);
                throw; // Re-throw to be caught by the constructor
            }
        }

        public void NavigateToCashierWithOrder(POS_UI.Models.OrderModel order)
        {
            var cashierPage = new POS_UI.CashierHomePage();
            var cashierVm = cashierPage.DataContext as POS_UI.ViewModels.CashierHomeViewModel;
            if (cashierVm != null)
            {
                cashierVm.LoadOrder(order);
            }
            this.MainFrame.Navigate(cashierPage);
        }

        private void OnConnectivityChanged(object sender, bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                if (!isConnected)
                {
                    // Save current navigation state before showing dialog
                    SaveCurrentNavigationState();
                    
                    // Show internet connection dialog as modal
                    if (_internetDialog == null || !_internetDialog.IsVisible)
                    {
                        _internetDialog = new InternetConnectionDialog(
                            onRetrySuccess: () => 
                            {
                                // Internet is back, restore navigation
                                RestoreNavigationState();
                            });
                        _internetDialog.ShowDialog(); // Show as modal dialog
                    }
                }
                else
                {
                    // Internet is back, close dialog if open
                    _internetDialog?.Close();
                    _internetDialog = null;
                }
            });
        }

        private void SaveCurrentNavigationState()
        {
            try
            {
                var currentPage = MainFrame.Content as Page;
                if (currentPage != null)
                {
                    var pageUri = currentPage.GetType().Name;
                    
                    // Convert page type to URI for navigation
                    string navigationUri = GetNavigationUriFromPageType(currentPage.GetType());
                    
                    if (!string.IsNullOrEmpty(navigationUri))
                    {
                        _navigationService.SaveNavigationState(navigationUri, pageUri);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save navigation state: {ex.Message}");
            }
        }

        private void RestoreNavigationState()
        {
            try
            {
                var navigationState = _navigationService.GetNavigationState();
                if (navigationState != null && !string.IsNullOrEmpty(navigationState.PageUri))
                {
                    MainFrame.Navigate(new Uri(navigationState.PageUri, UriKind.Relative));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore navigation state: {ex.Message}");
            }
        }

        private string GetNavigationUriFromPageType(Type pageType)
        {
            // Map page types to their navigation URIs
            var pageTypeName = pageType.Name;
            
            switch (pageTypeName)
            {
                case "CashierHomePage":
                    return "/View/CashierHomePage.xaml";
                case "AdminHomePage":
                    return "/View/AdminHomePage.xaml";
                case "KitchenPage":
                    return "/View/KitchenPage.xaml";
                case "LiveOrdersPage":
                    return "/View/LiveOrdersPage.xaml";
                case "TablesPage":
                    return "/View/TablesPage.xaml";
                case "InventoryPage":
                    return "/View/InventoryPage.xaml";
                case "SettingsPage":
                    return "/View/SettingsPage.xaml";
                case "HistoryPage":
                    return "/View/HistoryPage.xaml";
                case "LoginPage":
                    return "/View/LoginPage.xaml";
                default:
                    return "/View/CashierHomePage.xaml"; // Default fallback
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from connectivity changes
            _networkService.ConnectivityChanged -= OnConnectivityChanged;
            _networkService.StopMonitoring();
            base.OnClosed(e);
        }
    }
}