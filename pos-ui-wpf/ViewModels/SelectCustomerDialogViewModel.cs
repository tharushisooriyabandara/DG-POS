using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using POS_UI.Models;
using MaterialDesignThemes.Wpf;
using System;

namespace POS_UI.ViewModels
{
    public class SelectCustomerDialogViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CustomerModel> AllCustomers { get; set; }
        public ObservableCollection<CustomerModel> FilteredCustomers { get; set; }
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterCustomers();
            }
        }
        private CustomerModel _selectedCustomer;
        public CustomerModel SelectedCustomer
        {
            get => _selectedCustomer;
            set { _selectedCustomer = value; OnPropertyChanged(); }
        }
        
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }
        
        public ICommand ProceedCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand AddNewCustomerCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public SelectCustomerDialogViewModel(CustomerModel selected)
        {
            AllCustomers = new ObservableCollection<CustomerModel>();
            FilteredCustomers = new ObservableCollection<CustomerModel>();
            SelectedCustomer = selected;
            IsLoading = false;
            ProceedCommand = new RelayCommand(Proceed);
            CloseCommand = new RelayCommand(Close);
            AddNewCustomerCommand = new RelayCommand(async () => await AddNewCustomer());
            ClearSearchCommand = new RelayCommand(ClearSearch);
            LoadCustomersAsync();
        }
        private void FilterCustomers()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredCustomers = new ObservableCollection<CustomerModel>(AllCustomers);
            }
            else
            {
                var lower = SearchText.ToLower();
                FilteredCustomers = new ObservableCollection<CustomerModel>(AllCustomers.Where(c =>
                    (c.Name != null && c.Name.ToLower().Contains(lower)) ||
                    (c.Phone != null && c.Phone.ToLower().Contains(lower)) ||
                    (c.FullPhoneNumber != null && c.FullPhoneNumber.ToLower().Contains(lower))));
            }
            OnPropertyChanged(nameof(FilteredCustomers));
        }
        private void Proceed()
        {
            DialogHost.CloseDialogCommand.Execute(SelectedCustomer, null);
        }
        private void Close()
        {
            DialogHost.CloseDialogCommand.Execute(null, null);
        }
        private void ClearSearch()
        {
            SearchText = string.Empty;
        }
        private async System.Threading.Tasks.Task AddNewCustomer()
        {
            try
            {
                var dialogVm = new AddCustomerDialogViewModel();
                var dialog = new POS_UI.View.AddCustomerDialog { DataContext = dialogVm };
                // Show as modal and await result so we can propagate the created customer back
                // Open inside the nested DialogHost of SelectCustomerDialog to avoid 'already open' collisions
                var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "NestedCustomerDialogHost");
                if (result is CustomerModel created && created != null)
                {
                    SelectedCustomer = created;
                    // Close this dialog returning the newly created customer
                    DialogHost.CloseDialogCommand.Execute(SelectedCustomer, null);
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("dialog_error.log", $"[{DateTime.Now}] Error opening AddCustomerDialog: {ex}\n");
                System.Windows.MessageBox.Show($"Error opening Add Customer dialog: {ex.Message}", "Dialog Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async void LoadCustomersAsync()
        {
            try
            {
                IsLoading = true;
                
                // Check if user is authenticated
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    System.Windows.MessageBox.Show("You are not logged in. Please log in first.", "Authentication Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                
                var api = new POS_UI.Services.ApiService();
                var customers = await api.GetCustomersAsync();
                AllCustomers = new ObservableCollection<CustomerModel>(customers);
                FilteredCustomers = new ObservableCollection<CustomerModel>(AllCustomers);
                OnPropertyChanged(nameof(AllCustomers));
                OnPropertyChanged(nameof(FilteredCustomers));
            }
            catch (Exception ex)
            {
                // Check if it's a network connectivity issue
                var networkService = POS_UI.Services.NetworkConnectivityService.Instance;
                bool isNetworkError = !networkService.IsConnected || 
                                    ex.Message.Contains("Unable to connect") ||
                                    ex.Message.Contains("No such host") ||
                                    ex.Message.Contains("Connection refused") ||
                                    ex.Message.Contains("Network is unreachable") ||
                                    ex.Message.Contains("Timeout") ||
                                    ex.Message.Contains("The remote name could not be resolved") ||
                                    ex.Message.Contains("A connection attempt failed") ||
                                    ex.Message.Contains("The operation has timed out");
                
                if (isNetworkError)
                {
                    // Don't show error message if there's no internet connection
                    // The internet connection dialog will handle this
                    return;
                }
                
                if (ex.Message.Contains("Unauthorized") || ex.Message.Contains("401"))
                {
                    System.Windows.MessageBox.Show("Your session has expired. Please log in again.", "Session Expired", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
                else
                {
                    System.Windows.MessageBox.Show($"Failed to load customers: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    } 
}