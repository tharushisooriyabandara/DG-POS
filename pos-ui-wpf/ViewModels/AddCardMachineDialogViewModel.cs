using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using POS_UI.Models;
using System.Windows;
using System;
using System.Linq;
using System.Diagnostics;
using POS_UI.Services;
using System.Threading.Tasks;

namespace POS_UI.ViewModels
{
    public class AddCardMachineDialogViewModel : INotifyPropertyChanged
    {
        private bool _isEditMode;
        private bool _dialogResult;
        
        // Form Properties
        public string DeviceName { get; set; } = "";
        public string IPAddress { get; set; } = "";
        public string Port { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public string ParingCode { get; set; } = "";
        
        // Dropdown Properties
        public ObservableCollection<string> DeviceTypes { get; set; }
        public ObservableCollection<string> StatusOptions { get; set; }
        
        private string _selectedDeviceType;
        private string _selectedStatus;
        
        public string SelectedDeviceType
        {
            get => _selectedDeviceType;
            set
            {
                _selectedDeviceType = value;
                OnPropertyChanged();
            }
        }
        
        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                _selectedStatus = value;
                OnPropertyChanged();
            }
        }
        
        // UI Properties
        public string DialogTitle => IsEditMode ? "Edit Card Machine" : "Add Card Machine";
        public string SaveButtonText => IsEditMode ? "Update" : "Add";
        
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                _isEditMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DialogTitle));
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }
        
        public bool DialogResult
        {
            get => _dialogResult;
            set
            {
                _dialogResult = value;
                OnPropertyChanged();
            }
        }
        
        // Commands
        public ICommand CancelCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        
        public AddCardMachineDialogViewModel()
        {
            InitializeCollections();
            SetDefaults();
            InitializeCommands();
        }
        
        private void InitializeCollections()
        {
            DeviceTypes = new ObservableCollection<string>
            {
                "PaymentSave",
                "Ingenico",
                "Verifone",
                "PAX",
                "SumUp",
                "Square",
                "Custom"
            };
            
            StatusOptions = new ObservableCollection<string>
            {
                "Active",
                "Inactive"
            };
        }
        
        private void SetDefaults()
        {
            SelectedDeviceType = "PaymentSave";
            SelectedStatus = "Active";
            IPAddress = "192.168.1.8";
            Port = "8080";
            DeviceId = "1850095385";
            DeviceName = "PaymentSave123";
        }
        
        private void InitializeCommands()
        {
            CancelCommand = new RelayCommand(() => 
            {
                Debug.WriteLine("Cancel command executed");
                DialogResult = false;
            });

            SaveCommand = new AsyncRelayCommand(async () => await SaveAsync());
        }

        private async Task SaveAsync()
        {
            Debug.WriteLine("Save command executed");
            if (!ValidateInput())
                return;

            // Prepare endpoint
            var tempModel = new CardMachineModel
            {
                DeviceType = SelectedDeviceType
            };
            tempModel.SetDefaultAPIEndpoint();
            string apiEndpoint = tempModel.APIEndpoint;

            // Attempt pairing
            var api = new CardMachineApiService();
            try
            {
                string authToken = await api.PairDeviceAsync(IPAddress, Port, apiEndpoint, DeviceId, ParingCode);
                // Pairing succeeded, set auth token
                _pairingAuthToken = authToken;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pairing failed: {ex.Message}", "Pairing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Store the pairing token temporarily until CreateCardMachine is called
        private string _pairingAuthToken;

        public void SetAuthTokenForEdit(string authToken)
        {
            _pairingAuthToken = authToken;
        }

        public void PopulateFromCardMachine(CardMachineModel cardMachine)
        {
            if (cardMachine == null) return;
            
            DeviceName = cardMachine.DeviceName ?? "";
            SelectedDeviceType = cardMachine.DeviceType ?? "PaymentSave";
            IPAddress = cardMachine.IPAddress ?? "";
            Port = cardMachine.Port ?? "";
            DeviceId = cardMachine.DeviceId ?? "";
            ParingCode = cardMachine.ParingCode.ToString();
            SelectedStatus = cardMachine.IsActive ? "Active" : "Inactive";
            
            // Trigger property change notifications
            OnPropertyChanged(nameof(DeviceName));
            OnPropertyChanged(nameof(IPAddress));
            OnPropertyChanged(nameof(Port));
            OnPropertyChanged(nameof(DeviceId));
            OnPropertyChanged(nameof(ParingCode));
        }
        
        public CardMachineModel CreateCardMachine()
        {
            var cardMachine = new CardMachineModel
            {
                DeviceName = DeviceName?.Trim(),
                DeviceType = SelectedDeviceType,
                IPAddress = IPAddress?.Trim(),
                Port = Port?.Trim(),
                DeviceId = DeviceId?.Trim(),
                ParingCode = int.TryParse(ParingCode, out int code) ? code : 0,
                ParingCodeTime = DateTime.Now,
                IsActive = SelectedStatus == "Active",
                AuthToken = _pairingAuthToken // Set the token from pairing
            };
            
            // Set the API endpoint based on device type
            cardMachine.SetDefaultAPIEndpoint();
            
            return cardMachine;
        }
        
        public bool ValidateInput()
        {
            var errors = new System.Collections.Generic.List<string>();
            
            if (string.IsNullOrWhiteSpace(DeviceName))
                errors.Add("Device Name is required");
                
            if (string.IsNullOrWhiteSpace(SelectedDeviceType))
                errors.Add("Device Type is required");
                
            if (string.IsNullOrWhiteSpace(IPAddress))
                errors.Add("IP Address is required");
                
            if (string.IsNullOrWhiteSpace(Port))
                errors.Add("Port is required");
                
            if (string.IsNullOrWhiteSpace(DeviceId))
                errors.Add("Device ID is required");
                
            if (string.IsNullOrWhiteSpace(ParingCode))
                errors.Add("Pairing Code is required");
                
            if (string.IsNullOrWhiteSpace(SelectedStatus))
                errors.Add("Status is required");
            
            // Validate IP Address format
            if (!string.IsNullOrWhiteSpace(IPAddress) && !IsValidIPAddress(IPAddress))
                errors.Add("Invalid IP Address format");
            
            // Validate Port number
            if (!string.IsNullOrWhiteSpace(Port) && (!int.TryParse(Port, out int port) || port < 1 || port > 65535))
                errors.Add("Port must be a number between 1 and 65535");
            
            // Validate Pairing Code
            if (!string.IsNullOrWhiteSpace(ParingCode) && !int.TryParse(ParingCode, out int pairingCode))
                errors.Add("Pairing Code must be a number");
            
            if (errors.Any())
            {
                /*MessageBox.Show($"Please fix the following errors:\n\n{string.Join("\n", errors)}", 
                              "Validation Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Warning);*/
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Validation Failed", $"Please fix the following issue(s):\n{string.Join("\n", errors)}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddCardMachineDialogHost");
                return false;
            }
            
            return true;
        }
        
        private bool IsValidIPAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;
                
            var parts = ipAddress.Split('.');
            if (parts.Length != 4)
                return false;
                
            foreach (var part in parts)
            {
                if (!int.TryParse(part, out int num) || num < 0 || num > 255)
                    return false;
            }
            
            return true;
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 