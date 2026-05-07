using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS_UI.ViewModels;
using System.Text.RegularExpressions;
using POS_UI.Services;
using System;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace POS_UI
{
    public partial class LoginPage : Page
    {
        private LoginViewModel _viewModel;
        private readonly TokenValidationService _tokenValidationService;

        public LoginPage()
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            _tokenValidationService = new TokenValidationService();
            DataContext = _viewModel;
            _viewModel.LoginSucceeded += OnLoginSucceeded;
            Loaded += LoginPage_Loaded;
            Unloaded += LoginPage_Unloaded;
        }

        private void LoginPage_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.SetNavigationService(NavigationService);
            _viewModel.AttachIncomingOrdersListener();
            CheckTenantAndOutletCode();
            CheckExistingSession();
        }

        private void LoginPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.DetachIncomingOrdersListener();
        }

        private void CheckTenantAndOutletCode()
        {
            var settingsService = new POS_UI.Services.SettingsService();
            var (tenantCode, outletCode, brandId) = settingsService.LoadSettings();
            
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(tenantCode))
                missingFields.Add("Tenant Code");
            if (string.IsNullOrWhiteSpace(outletCode))
                missingFields.Add("Outlet Code");
            if (string.IsNullOrWhiteSpace(brandId))
                missingFields.Add("Brand Id");
            
            if (missingFields.Count > 0)
            {
                var missingFieldsText = string.Join(", ", missingFields);
                MessageBox.Show(
                    $"{missingFieldsText} {(missingFields.Count == 1 ? "is" : "are")} missing. Tap the ⚙️ Settings icon to configure them before proceeding.",
                    "Configuration Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void CheckExistingSession()
        {
            try
            {
                if (_tokenValidationService.IsTokenValid())
                {
                    var currentUser = _tokenValidationService.GetCurrentUser();
                    if (currentUser != null)
                    {
                        var role = currentUser.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                        if (!string.IsNullOrEmpty(role))
                        {
                            // Save navigation state for the appropriate page
                            var navigationService = new NavigationStateService();
                            if (role == "Admin")
                            {
                                navigationService.SaveNavigationState("/View/CashierHomePage.xaml", "CashierHomePage");
                                NavigationService?.Navigate(new Uri("/View/CashierHomePage.xaml", UriKind.Relative));
                            }
                            else if (role == "Cashier")
                            {
                                navigationService.SaveNavigationState("/View/CashierHomePage.xaml", "CashierHomePage");
                                NavigationService?.Navigate(new Uri("/View/CashierHomePage.xaml", UriKind.Relative));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking session: {ex.Message}", "Session Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ForgotPinCode_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Show appropriate dialog based on selected user's role
            if (_viewModel.SelectedUser != null)
            {
                if (_viewModel.SelectedUser.Role == "Admin" || _viewModel.SelectedUser.Role == "Outlet Admin")
                {
                    ShowAdminDialog();
                }
                else
                {
                    ShowCashierDialog();
                }
            }
            else
            {
                // Default to cashier dialog if no user is selected
                ShowCashierDialog();
            }
        }

        private void ShowAdminDialog()
        {
            DialogTitle.Text = "Admin PIN Recovery";
            DialogMessage.Text = "Admin passwords can only be retrieved via Support. Please contact our support team for assistance.";
            
            // Clear existing buttons and add admin button
            DialogButtons.Children.Clear();
            var supportButton = new Button
            {
                Content = "Contact Support",
                Style = FindResource("MaterialDesignRaisedButton") as Style,
                Width = 160,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            supportButton.Click += ContactSupport_Click;
            DialogButtons.Children.Add(supportButton);
            
            SupportDialogHost.IsOpen = true;
        }

        private void ShowCashierDialog()
        {
            DialogTitle.Text = "Cashier PIN Recovery";
            DialogMessage.Text = "To reset your PIN, please contact your outlet administrator.If your administrator is unavailable, you can reach support for further assistance.";
            
            // Clear existing buttons and add cashier buttons
            DialogButtons.Children.Clear();
            
            var adminButton = new Button
            {
                Content = "Contact Admin",
                Style = FindResource("MaterialDesignOutlinedButton") as Style,
                Width = 140,
                Margin = new Thickness(0, 0, 8, 0)
            };
            adminButton.Click += ContactSupport_Click;
            
            var supportButton = new Button
            {
                Content = "Contact Support",
                Style = FindResource("MaterialDesignRaisedButton") as Style,
                Width = 140,
                Margin = new Thickness(8, 0, 0, 0)
            };
            supportButton.Click += ContactSupport_Click;
            
            DialogButtons.Children.Add(adminButton);
            DialogButtons.Children.Add(supportButton);
            
            SupportDialogHost.IsOpen = true;
        }

        private void ContactSupport_Click(object sender, RoutedEventArgs e)
        {
            SupportDialogHost.IsOpen = false;
        }

        private void UserComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Open dropdown when clicking anywhere on the control, but don't swallow events while open
            if (sender is ComboBox combo)
            {
                if (!combo.IsDropDownOpen)
                {
                    combo.IsDropDownOpen = true;
                    e.Handled = true;
                }
            }
        }

        private void UserComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is not ComboBox combo)
                return;

            // Delay to ensure popup visual tree is created
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    var popup = combo.Template?.FindName("PART_Popup", combo) as Popup;
                    if (popup?.Child == null)
                        return;

                    // Let interactions inside the popup proceed unhindered
                    popup.PreviewMouseDown -= Popup_PreviewMouseDown;
                    popup.PreviewMouseDown += Popup_PreviewMouseDown;

                    var scrollViewer = FindVisualChild<ScrollViewer>(popup.Child);
                    if (scrollViewer == null)
                        return;

                    scrollViewer.Focusable = true;
                    scrollViewer.IsManipulationEnabled = true;
                    scrollViewer.PanningMode = PanningMode.VerticalOnly;
                    ScrollViewer.SetCanContentScroll(scrollViewer, true);
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                    scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

                    // Improve wheel scrolling reliability
                    scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
                    scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                }
                catch { /* ignore */ }
            }));
        }

        private void Popup_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Do not let page-level handlers mark popup interactions as handled
            e.Handled = false;
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                if (e.Delta < 0)
                    sv.LineDown();
                else
                    sv.LineUp();
                e.Handled = true;
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;
                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }

        //Allow only numbers in the textbox
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void OnLoginSucceeded()
        {
            Dispatcher.Invoke(() =>
            {
                // Navigate based on user role
                var navigationService = new NavigationStateService();
                
                if (_viewModel.SelectedUser != null)
                {
                    if (_viewModel.SelectedUser.Role == "Admin" || _viewModel.SelectedUser.Role == "Outlet Admin")
                    {
                        navigationService.SaveNavigationState("/View/CashierHomePage.xaml", "CashierHomePage");
                        NavigationService?.Navigate(new Uri("/View/CashierHomePage.xaml", UriKind.Relative));
                    }
                    else
                    {
                        navigationService.SaveNavigationState("/View/CashierHomePage.xaml", "CashierHomePage");
                        NavigationService?.Navigate(new Uri("/View/CashierHomePage.xaml", UriKind.Relative));
                    }
                }
                else
                {
                    // Default to cashier page if no user is selected
                    navigationService.SaveNavigationState("/View/CashierHomePage.xaml", "CashierHomePage");
                    NavigationService?.Navigate(new Uri("/View/CashierHomePage.xaml", UriKind.Relative));
                }
            });
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsDialog = new SettingsDialog();
            settingsDialog.Owner = Window.GetWindow(this);
            
            if (settingsDialog.ShowDialog() == true)
            {
                // Refresh the view model to use new settings
                _viewModel.RefreshApiService();
            }
        }
    }
} 