using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using POS_UI.Models;
using POS_UI.Services;
using MaterialDesignThemes.Wpf;
using POS_UI.View;
using System;

namespace POS_UI.ViewModels
{
    public class CustomerDetailsDialogViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private int _customerId;
        private string _fullName;
        private string _countryCode;
        private string _phone;
        private ObservableCollection<CustomerAddressModel> _addresses;
        private ObservableCollection<OrderModel> _orders;
        private bool _isLoading;

        public int CustomerId { get => _customerId; set { _customerId = value; OnPropertyChanged(); } }
        public string FullName { get => _fullName; set { _fullName = value; OnPropertyChanged(); } }
        public string CountryCode { get => _countryCode; set { _countryCode = value; OnPropertyChanged(); } }
        public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }
        public ObservableCollection<CustomerAddressModel> Addresses { get => _addresses; set { _addresses = value; OnPropertyChanged(); } }
        public ObservableCollection<OrderModel> Orders { get => _orders; set { _orders = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        public ICommand CloseCommand { get; }
        public ICommand ViewOrderCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public CustomerDetailsDialogViewModel(int customerId)
        {
            _apiService = new ApiService();
            _addresses = new ObservableCollection<CustomerAddressModel>();
            _orders = new ObservableCollection<OrderModel>();
            CloseCommand = new RelayCommand(Close);
            ViewOrderCommand = new RelayCommand<OrderModel>(async (order) => await ViewOrder(order));
            _ = LoadAsync(customerId);
        }

        private async Task LoadAsync(int customerId)
        {
            try
            {
                IsLoading = true;
                var details = await _apiService.GetCustomerByIdAsync(customerId);
                if (details?.Customer != null)
                {
                    CustomerId = details.Customer.CustomerId;
                    FullName = details.Customer.Name;
                    CountryCode = details.Customer.CountryCode;
                    Phone = details.Customer.Phone;
                    Addresses = new ObservableCollection<CustomerAddressModel>(details.Customer.Addresses ?? new System.Collections.Generic.List<CustomerAddressModel>());
                    Orders = new ObservableCollection<OrderModel>(details.Orders ?? new System.Collections.Generic.List<OrderModel>());
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load customer: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Close()
        {
            DialogHost.CloseDialogCommand.Execute(null, null);
        }

        private async Task ViewOrder(OrderModel order)
        {
            try
            {
                if (order == null) return;
                
                // Close the current customer details dialog first
                DialogHost.CloseDialogCommand.Execute(null, null);
                
                // Wait a moment for the dialog to close
                await Task.Delay(100);
                
                // Now open the order details dialog using the CustomerDialogHost
                var dialog = new POS_UI.View.KitchenOrderDetailsDialog
                {
                    DataContext = new POS_UI.ViewModels.KitchenOrderDetailsDialogViewModel(order.ApiId, POS_UI.ViewModels.KitchenOrderDetailsDialogViewModel.DialogMode.ViewOnly)
                };
                
                await DialogHost.Show(dialog, "CustomerDialogHost");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error showing order: {ex.Message}");
            }
        }
    }
}


