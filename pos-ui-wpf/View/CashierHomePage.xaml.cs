using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS_UI.ViewModels;
using POS_UI.Services;
using System.Threading.Tasks; // Added for Task.Delay
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Input.StylusPlugIns; // For Stylus touch configuration
using System.Collections.Generic; // For Dictionary
using System.Linq; // For LINQ operations

namespace POS_UI
{
    public partial class CashierHomePage : Page
    {
        private readonly TokenValidationService _tokenValidationService;

        public CashierHomePage()
        {
            InitializeComponent();
            if (this.DataContext == null)
            {
                this.DataContext = new CashierHomeViewModel();
            }
            _tokenValidationService = new TokenValidationService();
            Loaded += CashierHomePage_Loaded;
            //Unloaded += CashierHomePage_Unloaded;

            // Enable touch scrolling for both ScrollViewers
            EnableTouchScrolling();

            // Subscribe to transient alert requests to surface alert when away from Cashier
            /*try
            {
                GlobalDataService.Instance.TransientNewOrderAlertRequested += OnTransientNewOrderAlertRequested;
            }
            catch {  }*/
        }
        
        private void EnableTouchScrolling()
        {
            try
            {
                // Wait for layout to complete before accessing named elements
                this.Loaded += (s, e) =>
                {
                    try
                    {
                        // Find the ItemsControls inside the ScrollViewers
                        var categoriesItems = FindVisualChild<ItemsControl>(CategoriesScrollViewer);
                        var productsItems = FindVisualChild<ItemsControl>(ItemsScrollViewer);
                        
                        ConfigureScrollViewerForTouch(CategoriesScrollViewer, categoriesItems);
                        ConfigureScrollViewerForTouch(ItemsScrollViewer, productsItems);

                        if (MixedScrollViewer != null)
                        {
                            var mixedItems = FindVisualChild<ItemsControl>(MixedScrollViewer);
                            ConfigureScrollViewerForTouch(MixedScrollViewer, mixedItems);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Touch] Setup error: {ex.Message}");
                    }
                };
            }
            catch { /* Ignore touch setup errors */ }
        }
        
        private void ConfigureScrollViewerForTouch(ScrollViewer scrollViewer, ItemsControl itemsControl)
        {
            if (scrollViewer == null) return;
            
            try
            {
                // Disable Windows touch gestures that interfere
                Stylus.SetIsTapFeedbackEnabled(scrollViewer, false);
                Stylus.SetIsPressAndHoldEnabled(scrollViewer, false);
                Stylus.SetIsFlicksEnabled(scrollViewer, false);
                Stylus.SetIsTouchFeedbackEnabled(scrollViewer, false);
                
                // Variables for touch scrolling
                bool isScrolling = false;
                Point? lastPosition = null;
                
                // Use PreviewMouseDown/Move/Up - DON'T capture initially!
                scrollViewer.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    lastPosition = e.GetPosition(scrollViewer);
                    isScrolling = false;
                    // DON'T capture yet - let buttons receive events
                };
                
                scrollViewer.PreviewMouseMove += (s, e) =>
                {
                    if (lastPosition.HasValue && e.LeftButton == MouseButtonState.Pressed)
                    {
                        var currentPosition = e.GetPosition(scrollViewer);
                        var delta = currentPosition.Y - lastPosition.Value.Y;
                        
                        // Start scrolling if moved more than 20 pixels
                        if (!isScrolling && Math.Abs(delta) > 20)
                        {
                            isScrolling = true;
                            // NOW capture the mouse to ensure smooth scrolling
                            scrollViewer.CaptureMouse();
                        }
                        
                        if (isScrolling)
                        {
                            // Touch scrolling: drag up to scroll down (like pulling content up)
                            // Drag finger UP = content moves UP, revealing items below (increase offset)
                            // Drag finger DOWN = content moves DOWN, revealing items above (decrease offset)
                            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - delta);
                            lastPosition = currentPosition;
                            e.Handled = true;
                        }
                    }
                };
                
                scrollViewer.PreviewMouseLeftButtonUp += (s, e) =>
                {
                    if (isScrolling)
                    {
                        scrollViewer.ReleaseMouseCapture();
                        e.Handled = true;
                    }
                    // else: Don't handle it - let button receive the click!
                    
                    lastPosition = null;
                    isScrolling = false;
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Touch] Error configuring {scrollViewer.Name}: {ex.Message}");
            }
        }

        private async void CashierHomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_tokenValidationService.IsTokenValid())
            {
                MessageBox.Show("Your session has expired. Please login again.", "Session Expired", MessageBoxButton.OK, MessageBoxImage.Warning);
                NavigateToLogin();
                return;
            }

            var globalDataService = GlobalDataService.Instance;
            var hasOrderToLoad = globalDataService.CurrentOrderForEdit != null;
            var hasOngoingConfigOrder = hasOrderToLoad && globalDataService.HasOngoingOrderFromConfig;
            var cashierVm = this.DataContext as CashierHomeViewModel;

            if (hasOrderToLoad && cashierVm != null && !hasOngoingConfigOrder)
                cashierVm.IsFinishOrderLoading = true;

            try
            {
                try
                {
                    if (cashierVm != null)
                    {
                        System.Diagnostics.Debug.WriteLine("CashierHomePage: Checking if data refresh needed");
                        await cashierVm.RefreshDataIfNeededAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CashierHomePage: Data refresh failed: {ex.Message}");
                }

                try
                {
                    if (cashierVm != null)
                    {
                        System.Diagnostics.Debug.WriteLine("CashierHomePage: Starting API refresh for incoming orders");
                        await cashierVm.RefreshIncomingOrdersFromApiAsync();
                        System.Diagnostics.Debug.WriteLine("CashierHomePage: API refresh completed");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CashierHomePage: API refresh failed: {ex.Message}");
                }

                if (hasOrderToLoad && cashierVm != null)
                {
                    if (cashierVm.Tables == null || cashierVm.Tables.Count == 0)
                    {
                        Console.WriteLine("Tables not loaded yet, waiting for tables to load...");
                        await Task.Delay(1000);

                        if (cashierVm.Tables == null || cashierVm.Tables.Count == 0)
                        {
                            Console.WriteLine("Tables still not loaded, attempting to load manually...");
                            await cashierVm.LoadTablesFromApiAsync();
                        }
                    }

                    Console.WriteLine($"Tables loaded: {cashierVm.Tables?.Count ?? 0}");
                    cashierVm.LoadOrder(globalDataService.CurrentOrderForEdit);

                    // For ongoing_order restored from terminal config, treat as a fresh cart (Place Order flow)
                    if (hasOngoingConfigOrder)
                    {
                        cashierVm.IsOrderLoadedForEdit = false;
                        GlobalDataService.Instance.HasOngoingOrderFromConfig = false;
                    }
                    else
                    {
                        cashierVm.IsOrderLoadedForEdit = cashierVm.IsOrderLoadedForEdit;
                    }

                    globalDataService.CurrentOrderForEdit = null;
                }
            }
            finally
            {
                if (hasOrderToLoad && cashierVm != null && !hasOngoingConfigOrder)
                    cashierVm.IsFinishOrderLoading = false;
            }
        }

        /*private void CashierHomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            try { GlobalDataService.Instance.TransientNewOrderAlertRequested -= OnTransientNewOrderAlertRequested; } catch { }
        }
        */
        private void OnTransientNewOrderAlertRequested()
        {
            // If this page is currently visible, toggle the alert via VM
            if (this.DataContext is CashierHomeViewModel vm)
            {
                vm.IsOrderAlertVisible = false;
                vm.IsOrderAlertVisible = true;
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
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
                
                //clear draft ordrs
                var draftStorageService = new DraftStorageService();
                draftStorageService.ClearAllDrafts();
                
                // Clear navigation state on logout
                var navigationService = new NavigationStateService();
                navigationService.ClearNavigationState();
                
                // Navigate to login page
                NavigateToLogin();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during logout: {ex.Message}", "Logout Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToLogin()
        {
            try
            {
                if (NavigationService != null)
                {
                    NavigationService.Navigate(new Uri("/View/LoginPage.xaml", UriKind.Relative));
                }
                else
                {
                    MessageBox.Show("Navigation service is not available", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TablesButton_Click(object sender, RoutedEventArgs e)
        {
            // Notify VM before leaving Cashier via header Tables button
            if (this.DataContext is CashierHomeViewModel vm)
            {
                vm.HandleNavigatingAwayFromCashier();
            }
            var navigationService = new NavigationStateService();
            navigationService.SaveNavigationState("/View/TablesPage.xaml", "TablesPage");
            NavigationService?.Navigate(new Uri("/View/TablesPage.xaml", UriKind.Relative));
        }

        private void Button_Click()
        {

        }

        // Intercept dropdown click when only sentinel exists or sentinel selected to open modal directly
        private void AddressCombo_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is CashierHomeViewModel vm)
            {
                bool onlySentinel = vm.CustomerAddresses != null && vm.CustomerAddresses.Count == 1 && vm.CustomerAddresses[0].Id == 0;
                bool sentinelSelected = vm.SelectedAddress != null && vm.SelectedAddress.Id == 0;
                if (onlySentinel || sentinelSelected)
                {
                    e.Handled = true; // prevent dropdown from opening
                    vm.IsAddressDropdownOpen = false;
                    // Delegate to VM which opens dialog and handles save on "Use"
                    vm.OpenSelectAddressDialogCommand?.Execute(null);
                }
            }
        }

        private void AddressCombo_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is ComboBox cb)
            {
                // Run after the popup opens and item containers are generated
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        cb.UpdateLayout();
                        // Bring the first item (sentinel) into view to scroll to top
                        if (cb.Items.Count > 0)
                        {
                            var container = cb.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
                            container?.BringIntoView();
                        }

                        // Fallback: directly scroll the dropdown's ScrollViewer to top
                        var popup = cb.Template?.FindName("PART_Popup", cb) as Popup;
                        DependencyObject searchRoot = popup?.Child ?? (DependencyObject)cb;
                        var scrollViewer = FindVisualChild<ScrollViewer>(searchRoot);
                        scrollViewer?.ScrollToTop();
                    }
                    catch { /* ignore; best-effort across templates */ }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private async void AddressCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is CashierHomeViewModel vm)
            {
                // Ignore programmatic selection change during initialization or closing
                if (!cb.IsDropDownOpen) return;
                var newlySelected = cb.SelectedItem as Models.CustomerAddressModel;
                if (newlySelected != null && newlySelected.Id == 0)
                {
                    // Revert selection to previous item (if any)
                    var prev = e.RemovedItems != null && e.RemovedItems.Count > 0 ? e.RemovedItems[0] as Models.CustomerAddressModel : null;
                    cb.SelectionChanged -= AddressCombo_SelectionChanged;
                    cb.SelectedItem = prev;
                    vm.SelectedAddress = prev;
                    cb.SelectionChanged += AddressCombo_SelectionChanged;
                    cb.IsDropDownOpen = false;
                    vm.IsAddressDropdownOpen = false;

                    // Open modal directly via VM so it persists on "Use"
                    vm.OpenSelectAddressDialogCommand?.Execute(null);
                }
            }
        }

        private async System.Threading.Tasks.Task<object> ShowInDialogHostAsync(object dialog)
        {
            // Ensure we run on UI thread after layout so host is loaded
            return await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    return await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "AddItemDialogHost");
                }
                catch
                {
                    try
                    {
                        if (AddItemDialogHost != null)
                            return await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, AddItemDialogHost);
                    }
                    catch { }

                    // Last resort: implicit host
                    return await MaterialDesignThemes.Wpf.DialogHost.Show(dialog);
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded).Task;
        }

        private void CartScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (CartFadeOverlay == null) return;
            var sv = sender as ScrollViewer;
            if (sv == null) return;

            bool canScrollDown = sv.VerticalOffset + sv.ViewportHeight < sv.ExtentHeight - 1;
            CartFadeOverlay.Opacity = canScrollDown ? 1 : 0;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild) return tChild;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }

    // Code-behind partial class for button handler
    public partial class CashierHomePage
    {
        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            if (this.FindName("SearchTextBox") is TextBox tb)
            {
                tb.Clear();
            }
        }

        private void SortMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.FindName("SortPopup") is Popup popup)
            {
                popup.IsOpen = !popup.IsOpen;
            }
        }

        private void SortItemButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the popup when a sort item is clicked
            if (this.FindName("SortPopup") is Popup popup)
            {
                popup.IsOpen = false;
            }
            
            // Update the selected sort option
            if (sender is Button btn && btn.DataContext is ViewModels.ProductSortOption sortOption)
            {
                if (this.DataContext is CashierHomeViewModel vm)
                {
                    vm.SelectedSortOption = sortOption;
                }
            }
        }
    }
} 