using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using POS_UI.Models;
using POS_UI.Services;
using POS_UI.View;
using System.Linq;

namespace POS_UI.ViewModels
{
    public class AddCardMachineUserDialogViewModel : BaseViewModel
    {
        private readonly CardMachineModel _cardMachine;
        private readonly CardMachineUserService _userService;
        private readonly CardMachineApiService _apiService;
        private string _userId;
        private string _userName;
        private string _password;
        private bool _supervisor;

        public string UserId
        {
            get => _userId;
            set { _userId = value; OnPropertyChanged(); ((AsyncRelayCommand)AddUserCommand)?.RaiseCanExecuteChanged(); }
        }

        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); ((AsyncRelayCommand)AddUserCommand)?.RaiseCanExecuteChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); ((AsyncRelayCommand)AddUserCommand)?.RaiseCanExecuteChanged(); }
        }

        public bool Supervisor
        {
            get => _supervisor;
            set { _supervisor = value; OnPropertyChanged(); ((AsyncRelayCommand)AddUserCommand)?.RaiseCanExecuteChanged(); }
        }

        public ICommand AddUserCommand { get; }
        public ICommand CancelCommand { get; }

        public AddCardMachineUserDialogViewModel(CardMachineModel cardMachine, CardMachineUserService userService, CardMachineApiService apiService)
        {
            _cardMachine = cardMachine;
            _userService = userService;
            _apiService = apiService;

            AddUserCommand = new AsyncRelayCommand(AddUserAsync, CanAddUser);
            CancelCommand = new RelayCommand(Cancel);
        }

        private bool CanAddUser()
        {
            return !string.IsNullOrWhiteSpace(UserId) && 
                   !string.IsNullOrWhiteSpace(UserName) && 
                   !string.IsNullOrWhiteSpace(Password) &&
                   UserId.Length >= 4 && UserId.Length <= 8 &&
                   UserName.Length <= 8 &&
                   Password.Length >= 4 && Password.Length <= 8 &&
                   int.TryParse(UserId, out _) &&
                   int.TryParse(Password, out _);
        }

        private async Task AddUserAsync()
        {
            try
            {
                // Validate input
                if (!CanAddUser())
                {
                    MessageBox.Show("Please enter valid user information:\n- User ID: 4-8 digits\n- User Name: max 8 characters\n- Password: 4-8 digits", 
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Call API to create user
                var success = await _apiService.CreateUserAsync(
                    _cardMachine.IPAddress, 
                    _cardMachine.Port, 
                    _cardMachine.APIEndpoint, 
                    _cardMachine.DeviceId, 
                    UserId, 
                    UserName, 
                    Password, 
                    Supervisor,
                    _cardMachine.AuthToken);

                if (success)
                {
                    // Create user model and save to local storage (without password)
                    var user = new CardMachineUserModel
                    {
                        UserId = UserId,
                        UserName = UserName,
                        Password = "", // Don't save password
                        Supervisor = Supervisor,
                        Tid = _cardMachine.DeviceId,
                        CreatedAt = DateTime.Now
                    };

                    _userService.AddUser(_cardMachine.DeviceId, user);

                    MessageBox.Show($"User '{UserName}' created successfully!", 
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Close dialog
                    Application.Current.Windows.OfType<AddCardMachineUserDialog>().FirstOrDefault()?.Close();
                }
                else
                {
                    MessageBox.Show("Failed to create user. Please check the card machine connection and try again.", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating user: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel()
        {
            Application.Current.Windows.OfType<AddCardMachineUserDialog>().FirstOrDefault()?.Close();
        }
    }
} 