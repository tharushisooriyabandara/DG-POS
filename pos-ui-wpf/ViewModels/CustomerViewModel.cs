using POS_UI.Models;
using POS_UI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows;
using MaterialDesignThemes.Wpf;
using POS_UI.View;
using System;
using System.Collections.Generic;
using System.Linq;
using POS_UI.ViewModels;

namespace POS_UI.ViewModels
{
    public class CustomerViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private bool _isLoading;
        private bool _hasCustomers;
        private string _searchText;

        // Pagination backing fields
        private readonly ObservableCollection<CustomerModel> _customers;
        private List<CustomerModel> _allCustomers = new List<CustomerModel>();
        private int _pageSize = 10;
        private int _totalPages = 1;
        // Use a distinct name to avoid confusion with the sidebar's string CurrentPage
        private int _currentPagination = 1;

        public CustomerViewModel()
        {
            CurrentPage = "Customer";
            _apiService = new ApiService();
            _customers = new ObservableCollection<CustomerModel>();
            
            // Initialize commands
            AddCustomerCommand = new RelayCommand(AddCustomer);
            ViewCustomerCommand = new RelayCommand<CustomerModel>(ViewCustomer);
            EditCustomerCommand = new RelayCommand<CustomerModel>(EditCustomer);
            DeleteCustomerCommand = new RelayCommand<CustomerModel>(DeleteCustomer);
            _nextPageCommand = new RelayCommand(NextPage, CanGoNextPage);
            _prevPageCommand = new RelayCommand(PrevPage, CanGoPrevPage);
            _firstPageCommand = new RelayCommand(FirstPage, CanGoPrevPage);
            _lastPageCommand = new RelayCommand(LastPage, CanGoNextPage);
            
            // Load customers when view model is created
            _ = LoadCustomersAsync();
        }

        // Exposed collection for the current page
        public ObservableCollection<CustomerModel> Customers => _customers;
        public string CurrentPage { get; set; }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                PerformSearch();
            }
        }

        private void PerformSearch()
        {
            // Get filtered customers and update pagination
            var filteredCustomers = GetCurrentFilteredCustomers();
            RecalculatePaging(filteredCustomers, resetToFirstPage: true);
        }

        private List<CustomerModel> GetCurrentFilteredCustomers()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return _allCustomers;
            }
            
            var searchLower = SearchText.ToLower();
            return _allCustomers.Where(customer =>
                (customer.CustomerId.ToString().Contains(searchLower)) ||
                (customer.Name?.ToLower().Contains(searchLower) ?? false) ||
                (customer.Phone?.ToLower().Contains(searchLower) ?? false) ||
                (customer.FullPhoneNumber?.ToLower().Contains(searchLower) ?? false)
            ).ToList();
        }

        public bool HasCustomers
        {
            get => _hasCustomers;
            set
            {
                _hasCustomers = value;
                OnPropertyChanged();
            }
        }

        // Commands
        public ICommand AddCustomerCommand { get; }
        public ICommand ViewCustomerCommand { get; }
        public ICommand EditCustomerCommand { get; }
        public ICommand DeleteCustomerCommand { get; }
        private readonly RelayCommand _nextPageCommand;
        private readonly RelayCommand _prevPageCommand;
        private readonly RelayCommand _firstPageCommand;
        private readonly RelayCommand _lastPageCommand;
        public ICommand NextPageCommand => _nextPageCommand;
        public ICommand PrevPageCommand => _prevPageCommand;
        public ICommand FirstPageCommand => _firstPageCommand;
        public ICommand LastPageCommand => _lastPageCommand;

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (value <= 0) return;
                if (_pageSize == value) return;
                _pageSize = value;
                OnPropertyChanged();
                RecalculatePaging(resetToFirstPage: true);
            }
        }

        public int CurrentPagination
        {
            get => _currentPagination;
            private set
            {
                if (_currentPagination == value) return;
                _currentPagination = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageInfoText));
                RefreshPaginationCommands();
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                if (_totalPages == value) return;
                _totalPages = Math.Max(1, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageInfoText));
                RefreshPaginationCommands();
            }
        }

        public string PageInfoText => $"Page {CurrentPagination} of {TotalPages}";

        private async Task LoadCustomersAsync()
        {
            try
            {
                IsLoading = true;
                var customers = await _apiService.GetCustomersAsync();

                _allCustomers = customers ?? new List<CustomerModel>();
                HasCustomers = _allCustomers.Count > 0;
                RecalculatePaging(resetToFirstPage: true);
            }
            catch (System.Exception ex)
            {
                //MessageBox.Show($"Error loading customers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void RecalculatePaging(List<CustomerModel> customers, bool resetToFirstPage)
        {
            // Compute total pages
            int count = customers?.Count ?? 0;
            TotalPages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
            if (resetToFirstPage)
            {
                CurrentPagination = 1;
            }
            else if (CurrentPagination > TotalPages)
            {
                CurrentPagination = TotalPages;
            }
            UpdatePagedCustomers(customers);
        }

        private void RecalculatePaging(bool resetToFirstPage)
        {
            // Overload for backward compatibility - uses all customers
            RecalculatePaging(_allCustomers, resetToFirstPage);
        }

        private void UpdatePagedCustomers(List<CustomerModel> customers = null)
        {
            _customers.Clear();
            var customersToUse = customers ?? _allCustomers;
            if (customersToUse == null || customersToUse.Count == 0)
            {
                return;
            }

            int skip = (CurrentPagination - 1) * PageSize;
            foreach (var customer in customersToUse.Skip(skip).Take(PageSize))
            {
                _customers.Add(customer);
            }
        }

        private void RefreshPaginationCommands()
        {
            _nextPageCommand.RaiseCanExecuteChanged();
            _prevPageCommand.RaiseCanExecuteChanged();
            _firstPageCommand.RaiseCanExecuteChanged();
            _lastPageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoNextPage() => CurrentPagination < TotalPages;
        private bool CanGoPrevPage() => CurrentPagination > 1;
        private void NextPage()
        {
            if (!CanGoNextPage()) return;
            CurrentPagination++;
            UpdatePagedCustomers(GetCurrentFilteredCustomers());
        }
        private void PrevPage()
        {
            if (!CanGoPrevPage()) return;
            CurrentPagination--;
            UpdatePagedCustomers(GetCurrentFilteredCustomers());
        }
        private void FirstPage()
        {
            if (!CanGoPrevPage()) return;
            CurrentPagination = 1;
            UpdatePagedCustomers(GetCurrentFilteredCustomers());
        }
        private void LastPage()
        {
            if (!CanGoNextPage()) return;
            CurrentPagination = TotalPages;
            UpdatePagedCustomers(GetCurrentFilteredCustomers());
        }

        private void AddCustomer()
        {
            // TODO: Implement add customer functionality
            MessageBox.Show("Add Customer functionality will be implemented later.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void ViewCustomer(CustomerModel customer)
        {
            if (customer == null) return;
            try
            {
                var vm = new POS_UI.ViewModels.CustomerDetailsDialogViewModel(customer.CustomerId);
                var dialog = new POS_UI.View.CustomerDetailsDialog { DataContext = vm };
                await DialogHost.Show(dialog, "CustomerDialogHost");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening customer details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void EditCustomer(CustomerModel customer)
        {
            if (customer == null) return;
            
            try
            {
                var vm = new EditCustomerDialogViewModel(customer.CustomerId);
                var dialog = new EditCustomerDialog { DataContext = vm };
                await DialogHost.Show(dialog, "CustomerDialogHost");
                
                // Refresh the customer list after editing
                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening edit customer dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteCustomer(CustomerModel customer)
        {
            if (customer == null) return;
            
            // TODO: Implement delete customer functionality
            MessageBox.Show($"Delete Customer: {customer.Name} (ID: {customer.CustomerId})", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
