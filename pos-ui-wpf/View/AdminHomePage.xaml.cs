using System;
using System.Windows;
using System.Windows.Controls;
using POS_UI.Services;
using System.Security.Claims;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for AdminHomePage.xaml
    /// </summary>
    public partial class AdminHomePage : Page
    {
        private readonly TokenValidationService _tokenValidationService;

        public AdminHomePage()
        {
            InitializeComponent();
            _tokenValidationService = new TokenValidationService();
            Loaded += AdminHomePage_Loaded;
            CheckTokenStatus();
        }

        private void AdminHomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_tokenValidationService.IsTokenValid())
            {
                MessageBox.Show("Your session has expired. Please login again.", "Session Expired", MessageBoxButton.OK, MessageBoxImage.Warning);
                NavigateToLogin();
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear all tokens
                _tokenValidationService.ClearTokens();
                
                // Clear global data (current user and shop details)
                var globalDataService = GlobalDataService.Instance;
                globalDataService.ClearData();
                
                // Clear navigation state on logout
                //var navigationService = new NavigationStateService();
                //navigationService.ClearNavigationState();
                
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

        private void CheckTokenStatus()
        {
            var currentUser = _tokenValidationService.GetCurrentUser();
            if (currentUser != null)
            {
                var userId = currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = currentUser.FindFirst(ClaimTypes.Email)?.Value;
                var role = currentUser.FindFirst(ClaimTypes.Role)?.Value;
                var name = currentUser.FindFirst(ClaimTypes.Name)?.Value;

                var message = $"Token Status:\n\n" +
                            $"User ID: {userId}\n" +
                            $"Name: {name}\n" +
                            $"Email: {email}\n" +
                            $"Role: {role}\n\n" +
                            $"Access Token Valid: {_tokenValidationService.IsTokenValid()}\n" +
                            $"Refresh Token Valid: {_tokenValidationService.IsRefreshTokenValid()}";

                MessageBox.Show(message, "Token Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No valid token found. Please login again.", "Token Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            NavigationService?.Navigate(new Uri("/View/LoginPage.xaml", UriKind.Relative));
            }
        }
    }
}
