using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using POS_UI.Models;
using POS_UI.Services;

namespace POS_UI.ViewModels
{
    public class AdminCredentialsDialogViewModel : BaseViewModel
    {
        private UserModel _selectedUser;
        private bool _isLoading;
        private string _errorMessage;
        private bool _isClearingPinBoxes;

        public ObservableCollection<UserModel> Users { get; set; }
        public ObservableCollection<PinBoxViewModel> PinBoxes { get; set; }
        
        public UserModel SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (_selectedUser != value)
                {
                    _selectedUser = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanGetAccess));
                    if (GetAccessCommand is RelayCommand relayCmd)
                    {
                        relayCmd.RaiseCanExecuteChanged();
                    }
                    // Clear PIN boxes and error when user changes
                    ClearPinBoxes();
                    ClearError();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        public string Password
        {
            get => string.Join("", PinBoxes.Select(p => p.Text));
        }

        public bool CanGetAccess => SelectedUser != null && PinBoxes.All(box => !string.IsNullOrEmpty(box.Text));

        public ICommand GetAccessCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand KeypadCommand { get; }
        public ICommand DeleteLastDigitCommand { get; }
        public ICommand ClearErrorCommand { get; }

        public AdminCredentialsDialogViewModel()
        {
            Users = new ObservableCollection<UserModel>();
            PinBoxes = new ObservableCollection<PinBoxViewModel>();
            
            // Initialize with 6 PIN boxes for admin users (admins use 6-digit PINs)
            InitializePinBoxes(6);
            
            GetAccessCommand = new RelayCommand(
                () => GetAccess(),
                () => CanGetAccess);
            CancelCommand = new RelayCommand(() => Cancel());
            KeypadCommand = new RelayCommand<string>(digit => ExecuteKeypad(digit));
            DeleteLastDigitCommand = new RelayCommand(() => DeleteLastDigit());
            ClearErrorCommand = new RelayCommand(() => ClearError());
            
            // Load admin users from API
            _ = LoadAdminUsersAsync();
        }

        private void InitializePinBoxes(int count)
        {
            PinBoxes.Clear();
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
                OnPropertyChanged(nameof(Password));
                OnPropertyChanged(nameof(CanGetAccess));
                if (GetAccessCommand is RelayCommand relayCmd)
                {
                    relayCmd.RaiseCanExecuteChanged();
                }
                // Clear error when user starts typing (but not when we're programmatically clearing)
                if (!_isClearingPinBoxes && !string.IsNullOrEmpty(ErrorMessage))
                {
                    ClearError();
                }
            }
        }

        private async Task LoadAdminUsersAsync()
        {
            IsLoading = true;
            try
            {
                var apiService = new ApiService();
                // Request only admin users using the roles parameter
                // Pass comma-separated roles: "Admin,OutletAdmin"
                var adminUsers = await apiService.GetUsersAsync(roles: 4);
                
                // Sort by full name
                var sortedUsers = adminUsers
                    .OrderBy(u => u.FullName)
                    .ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Users.Clear();
                    foreach (var user in sortedUsers)
                    {
                        Users.Add(user);
                    }
                    
                    // Set the first admin user as default if available
                    if (Users.Count > 0)
                    {
                        SelectedUser = Users[0];
                    }
                });
            }
            catch (Exception ex)
            {
                // Show error message
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Failed to load admin users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecuteKeypad(string digit)
        {
            if (int.TryParse(digit, out _))
            {
                // Clear error when user starts entering PIN
                if (!string.IsNullOrEmpty(ErrorMessage))
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

        private void ClearPinBoxes()
        {
            _isClearingPinBoxes = true;
            try
            {
                foreach (var box in PinBoxes)
                {
                    box.Text = string.Empty;
                }
            }
            finally
            {
                _isClearingPinBoxes = false;
            }
        }

        private void ClearError()
        {
            ErrorMessage = string.Empty;
        }

        private async void GetAccess()
        {
            if (SelectedUser == null || string.IsNullOrEmpty(Password))
            {
                MessageBox.Show("Please select a user and enter PIN.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsLoading = true;
            try
            {
                var apiService = new ApiService();
                var settingsService = new SettingsService();
                var (_, outletCode, _) = settingsService.LoadSettings();

                if (string.IsNullOrEmpty(outletCode))
                {
                    MessageBox.Show("Outlet code is not configured. Please check settings.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Verify PIN using the API
                var isValid = await apiService.VerifyPinAsync(outletCode, SelectedUser.Email, Password);

                if (isValid)
                {
                    // PIN is valid, clear any error and close dialog with success
                    ClearError();
                    CloseDialog(true);
                }
                else
                {
                    // PIN is invalid - clear PIN boxes first, then show error message
                    ClearPinBoxes();
                    ErrorMessage = "Invalid PIN. Please try again.";
                }
            }
            catch (Exception ex)
            {
                // Clear PIN boxes first, then show error message
                ClearPinBoxes();
                ErrorMessage = "Invalid PIN. Please try again.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Cancel()
        {
            ClearPinBoxes();
            CloseDialog(false);
        }

        private void CloseDialog(bool result)
        {
            try
            {
                // Try to close using RootDialog first (most common)
                if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("RootDialog"))
                {
                    MaterialDesignThemes.Wpf.DialogHost.Close("RootDialog", result);
                    return;
                }
                // Try RootDialogHost as fallback
                if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("RootDialogHost"))
                {
                    MaterialDesignThemes.Wpf.DialogHost.Close("RootDialogHost", result);
                    return;
                }
                // Fallback to generic close command
                MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(result, null);
            }
            catch (Exception)
            {
                // If DialogHost is not available, try alternative approach
                try
                {
                    MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(result, null);
                }
                catch
                {
                    // Silently ignore if dialog host is not available
                }
            }
        }
    }
}
