using System;
using System.Collections.ObjectModel;
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
    public class CardMachineUsersDialogViewModel : BaseViewModel
    {
        private readonly CardMachineModel _cardMachine;
        private readonly CardMachineUserService _userService;
        private readonly CardMachineApiService _apiService;
        private ObservableCollection<CardMachineUserModel> _users;
        private string _dialogTitle;

        public ObservableCollection<CardMachineUserModel> Users
        {
            get => _users;
            set { _users = value; OnPropertyChanged(); }
        }

        public string DialogTitle
        {
            get => _dialogTitle;
            set { _dialogTitle = value; OnPropertyChanged(); }
        }

        public ICommand AddUserCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand CloseCommand { get; }

        public CardMachineUsersDialogViewModel(CardMachineModel cardMachine)
        {
            _cardMachine = cardMachine;
            _userService = CardMachineUserService.Instance;
            _apiService = new CardMachineApiService();
            
            DialogTitle = $"Users for {cardMachine.DeviceName} ({cardMachine.DeviceId})";
            Users = _userService.GetUsersForTerminal(cardMachine.DeviceId);

            AddUserCommand = new RelayCommand(AddUser);
            DeleteUserCommand = new RelayCommand<CardMachineUserModel>(DeleteUser);
            CloseCommand = new RelayCommand(Close);
        }

        private void AddUser()
        {
            var addUserViewModel = new AddCardMachineUserDialogViewModel(_cardMachine, _userService, _apiService);
            var addUserDialog = new AddCardMachineUserDialog(addUserViewModel);
            
            addUserDialog.Owner = Application.Current.MainWindow;
            addUserDialog.ShowDialog();
            
            // Refresh the users list after adding
            Users = _userService.GetUsersForTerminal(_cardMachine.DeviceId);
        }

        private async void DeleteUser(CardMachineUserModel user)
        {
            if (user == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete user '{user.UserName}'?", 
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Call API to delete user from card machine
                    var success = await _apiService.DeleteUserAsync(
                        _cardMachine.IPAddress,
                        _cardMachine.Port,
                        _cardMachine.APIEndpoint,
                        _cardMachine.DeviceId,
                        user.UserId,
                        _cardMachine.AuthToken);

                    if (success)
                    {
                        // Remove from local storage only if API call succeeds
                        _userService.DeleteUser(_cardMachine.DeviceId, user);
                        Users = _userService.GetUsersForTerminal(_cardMachine.DeviceId);
                        
                        MessageBox.Show($"User '{user.UserName}' deleted successfully!", 
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete user from card machine. Please check the connection and try again.", 
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting user: {ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Close()
        {
            Application.Current.Windows.OfType<CardMachineUsersDialog>().FirstOrDefault()?.Close();
        }
    }
} 