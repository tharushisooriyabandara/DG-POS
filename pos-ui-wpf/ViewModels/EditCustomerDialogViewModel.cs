using POS_UI.Models;
using POS_UI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using POS_UI.Services;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace POS_UI.ViewModels
{
    public class EditCustomerDialogViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly int _customerId;
        private bool _isLoading;

        public EditCustomerDialogViewModel(int customerId)
        {
            _apiService = new ApiService();
            _customerId = customerId;
            Addresses = new ObservableCollection<CustomerAddressModel>();
            Addresses.CollectionChanged += (s, e) =>
            {
                HasAnyAddress = Addresses != null && Addresses.Count > 0;
            };
            
            // Initialize commands
            UpdateCustomerCommand = new RelayCommand(async () => await UpdateCustomerAsync());
            CancelCommand = new RelayCommand(Cancel);
            AddAddressCommand = new RelayCommand(AddAddress);
            RemoveAddressCommand = new RelayCommand<CustomerAddressModel>(RemoveAddress);
            SaveNewAddressCommand = new RelayCommand(async () => await SaveNewAddressAsync());
            ClearPlaceSearchCommand = new RelayCommand(() =>
            {
                PlaceSearchText = string.Empty;
                PlacePredictions.Clear();
                OnPropertyChanged(nameof(ArePredictionsVisible));
            });
            
            // Initialize new address
            NewAddress = new CustomerAddressModel();
            ShowNewAddressForm = false;
            HasAnyAddress = false;
            
            // Load customer data
            _ = LoadCustomerDataAsync();
        }

        // Properties
        private string _firstName;
        public string FirstName
        {
            get => _firstName;
            set
            {
                _firstName = value;
                OnPropertyChanged();
            }
        }

        private string _lastName;
        public string LastName
        {
            get => _lastName;
            set
            {
                _lastName = value;
                OnPropertyChanged();
            }
        }

        private string _countryCode;
        public string CountryCode
        {
            get => _countryCode;
            set
            {
                _countryCode = value;
                OnPropertyChanged();
            }
        }

        private string _phone;
        public string Phone
        {
            get => _phone;
            set
            {
                _phone = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<CustomerAddressModel> Addresses { get; }

        private CustomerAddressModel _newAddress;
        public CustomerAddressModel NewAddress
        {
            get => _newAddress;
            set
            {
                _newAddress = value;
                OnPropertyChanged();
            }
        }

        private bool _hasAnyAddress;
        public bool HasAnyAddress
        {
            get => _hasAnyAddress;
            set
            {
                if (_hasAnyAddress == value) return;
                _hasAnyAddress = value;
                OnPropertyChanged();
            }
        }

        private bool _showNewAddressForm;
        public bool ShowNewAddressForm
        {
            get => _showNewAddressForm;
            set
            {
                if (_showNewAddressForm == value) return;
                _showNewAddressForm = value;
                OnPropertyChanged();
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

        // Google Places - Autocomplete for Address Line 1 (New address form)
        private string _placeSearchText;
        private bool _suppressPredictionFetch;
        public string PlaceSearchText
        {
            get => _placeSearchText;
            set
            {
                if (_placeSearchText == value) return;
                _placeSearchText = value;
                OnPropertyChanged();
                if (!_suppressPredictionFetch)
                {
                    _ = FetchPlacePredictionsAsync(_placeSearchText);
                }
            }
        }

        public ObservableCollection<string> PlacePredictions { get; } = new ObservableCollection<string>();
        public bool ArePredictionsVisible => PlacePredictions.Count > 0;

        private string _selectedPrediction;
        public string SelectedPrediction
        {
            get => _selectedPrediction;
            set
            {
                if (_selectedPrediction == value) return;
                _selectedPrediction = value;
                OnPropertyChanged();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _suppressPredictionFetch = true;
                    PlaceSearchText = value;
                    _suppressPredictionFetch = false;
                    _ = ResolveSelectedPlaceAsync(value);
                    PlacePredictions.Clear();
                    OnPropertyChanged(nameof(ArePredictionsVisible));
                }
            }
        }

        // Commands
        public ICommand UpdateCustomerCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddAddressCommand { get; }
        public ICommand RemoveAddressCommand { get; }
        public ICommand SaveNewAddressCommand { get; }
        public ICommand ClearPlaceSearchCommand { get; }

        private async Task LoadCustomerDataAsync()
        {
            try
            {
                IsLoading = true;
                var customerDetail = await _apiService.GetCustomerByIdAsync(_customerId);
                
                if (customerDetail?.Customer != null)
                {
                    var customer = customerDetail.Customer;
                    FirstName = customer.FirstName;
                    LastName = customer.LastName;
                    CountryCode = customer.CountryCode;
                    Phone = customer.Phone;

                    // Load addresses
                    Addresses.Clear();
                    if (customer.Addresses != null)
                    {
                        foreach (var address in customer.Addresses)
                        {
                            Addresses.Add(address);
                        }
                    }
                    HasAnyAddress = Addresses.Count > 0;
                    ShowNewAddressForm = false;
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error loading customer data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Loading Customer Data", $"Error loading customer data: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                 // Close the current dialog first, then show the error message
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MaterialDesignThemes.Wpf.DialogHost.Close("CustomerDialogHost", null);
                });
                
                // Wait a moment for the dialog to close, then show error message
                await Task.Delay(100);
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "CustomerDialogHost");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task UpdateCustomerAsync()
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(FirstName))
                {
                    //MessageBox.Show("First name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Validation", "First name is required.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                     // Close the current dialog first, then show the error message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("CustomerDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "CustomerDialogHost");
                    return;
                }

                if (string.IsNullOrWhiteSpace(LastName))
                {
                    //MessageBox.Show("Last name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Validation", "Last name is required.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                     // Close the current dialog first, then show the error message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("CustomerDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "CustomerDialogHost");
                    return;
                }

                if (string.IsNullOrWhiteSpace(CountryCode))
                {
                    //MessageBox.Show("Country code is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Validation", "Country code is required.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                     // Close the current dialog first, then show the error message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("CustomerDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "CustomerDialogHost");
                    return;
                }

                if (string.IsNullOrWhiteSpace(Phone))
                {
                    //MessageBox.Show("Phone number is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Validation", "Phone number is required.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                     // Close the current dialog first, then show the error message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("CustomerDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "CustomerDialogHost");
                    return;
                }

                // Validate addresses
                foreach (var address in Addresses)
                {
                    if (string.IsNullOrWhiteSpace(address.AddressLine1) || string.IsNullOrWhiteSpace(address.Label))
                    {
                        //MessageBox.Show("Address Line 1 is required for all addresses.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Validation", "Address Line 1 and Label are required for all addresses.");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                         // Close the current dialog first, then show the error message
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MaterialDesignThemes.Wpf.DialogHost.Close("CustomerDialogHost", null);
                        });
                        
                        // Wait a moment for the dialog to close, then show error message
                        await Task.Delay(100);
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "CustomerDialogHost");
                        return;
                    }
                }

                IsLoading = true;

                // Create the request model
                var updateRequest = new POS_UI.Models.CustomerUpdateRequestModel
                {
                    FirstName = FirstName,
                    LastName = LastName,
                    CountryCode = CountryCode,
                    Phone = Phone,
                    Addresses = Addresses.ToList()
                };

                // Call the API to update customer
                var success = await _apiService.UpdateCustomerAsync(_customerId, updateRequest);

                if (success)
                {
                    //DialogHost.Close("CustomerDialogHost");
                    //MessageBox.Show("Customer updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information); 
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Success", "Customer updated successfully!");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                     // Close the current dialog first, then show the error message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("CustomerDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "CustomerDialogHost");
                }
                else
                {
                    //MessageBox.Show("Failed to update customer. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", "Failed to update customer. Please try again.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                     // Close the current dialog first, then show the error message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("CustomerDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "CustomerDialogHost");
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error updating customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                try
                {
                    // Extract JSON from the exception message
                    string jsonPart = ex.Message;
                    int jsonStart = ex.Message.IndexOf("{");
                    if (jsonStart >= 0)
                    {
                        jsonPart = ex.Message.Substring(jsonStart);
                    }
                    
                    // Try to parse the error response as JSON using dynamic parsing
                    var jsonObject = JsonConvert.DeserializeObject<dynamic>(jsonPart);
                    string header = jsonObject?.message ?? "Customer Update Failed";
                    string errorDetails = "An unexpected error occurred. Please try again.";
                    
                    // Handle different error formats
                    if (jsonObject?.errors != null)
                    {
                        if (jsonObject.errors is Newtonsoft.Json.Linq.JObject)
                        {
                            // Errors is an object/dictionary
                            var errorsDict = jsonObject.errors.ToObject<Dictionary<string, string>>();
                            if (errorsDict != null && errorsDict.Count > 0)
                            {
                                errorDetails = string.Join("\n", errorsDict.Values);
                            }
                        }
                        else if (jsonObject.errors is Newtonsoft.Json.Linq.JValue)
                        {
                            // Errors is a JValue (primitive value)
                            errorDetails = jsonObject.errors.ToString();
                        }
                        else if (jsonObject.errors is string)
                        {
                            // Errors is a string
                            errorDetails = jsonObject.errors.ToString();
                        }
                        else if (jsonObject.errors is Newtonsoft.Json.Linq.JArray)
                        {
                            // Errors is an array
                            var errorsArray = jsonObject.errors.ToObject<string[]>();
                            if (errorsArray != null && errorsArray.Length > 0)
                            {
                                errorDetails = string.Join("\n", errorsArray);
                            }
                        }
                    }

                    string friendlyText = Regex.Replace(errorDetails, "(\\B[A-Z])", " $1");

                    // Use the API message as header and errors as message
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError(header, friendlyText);
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };

                    // Close the current dialog first, then show the error message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("CustomerDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "CustomerDialogHost");
                }
                catch
                {
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", $"Error updating customer: {ex.Message}");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    // Close the current dialog first, then show the error message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("CustomerDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "CustomerDialogHost");
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Cancel()
        {
            DialogHost.Close("CustomerDialogHost");
        }

        private void AddAddress()
        {
            // Show the new address form
            ShowNewAddressForm = true;
            // Reset the form
            NewAddress = new CustomerAddressModel
            {
                IsDefault = Addresses.Count == 0 // First address is default
            };
        }

        private async Task SaveNewAddressAsync()
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(NewAddress.AddressLine1) || string.IsNullOrWhiteSpace(NewAddress.Label))
                {
                    // Show validation error in nested dialog without closing the parent Edit Customer modal
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Validation", "Address Line 1 and Label are required.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "EditCustomerDialogHost");
                    return;
                }

                IsLoading = true;

                // Call the API to add the address
                var success = await _apiService.CreateCustomerAddressAsync(_customerId, NewAddress);

                if (success)
                {
                    // Reload customer data to get the updated addresses
                    await LoadCustomerDataAsync();
                    
                    // Hide form after success
                    ShowNewAddressForm = false;
                    HasAnyAddress = Addresses.Count > 0;
                    
                    // Show success message in nested dialog without closing the parent Edit Customer modal
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Success", "Address added successfully!");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    // Use the nested DialogHost instead of closing the parent
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "EditCustomerDialogHost");
                }
                else
                {
                    // Show error message in nested dialog without closing the parent Edit Customer modal
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", "Failed to add address. Please try again.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "EditCustomerDialogHost");
                }
            }
            catch (Exception ex)
            {
                // Show error message in nested dialog without closing the parent Edit Customer modal
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", $"Error adding address: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "EditCustomerDialogHost");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void RemoveAddress(CustomerAddressModel address)
        {
            if (address != null)
            {
                Addresses.Remove(address);
                HasAnyAddress = Addresses.Count > 0;
            }
        }

        private async Task FetchPlacePredictionsAsync(string input)
        {
            try
            {
                PlacePredictions.Clear();
                if (string.IsNullOrWhiteSpace(input) || input.Trim().Length < 3)
                {
                    OnPropertyChanged(nameof(ArePredictionsVisible));
                    return;
                }
                var country = GlobalDataService.Instance.ShopDetails?.CountryCode ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(country) && country.Length > 2)
                {
                    var dash = country.IndexOf('-');
                    country = dash > 0 ? country.Substring(0, dash) : country;
                }
                var list = await _apiService.GoogleGetPredictionsAsync(input.Trim(), country);
                foreach (var p in list)
                {
                    PlacePredictions.Add(p);
                }
                OnPropertyChanged(nameof(ArePredictionsVisible));
            }
            catch
            {
                OnPropertyChanged(nameof(ArePredictionsVisible));
            }
        }

        private async Task ResolveSelectedPlaceAsync(string description)
        {
            try
            {
                var country = GlobalDataService.Instance.ShopDetails?.CountryCode ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(country) && country.Length > 2)
                {
                    var dash = country.IndexOf('-');
                    country = dash > 0 ? country.Substring(0, dash) : country;
                }
                var (_, lat, lng, formatted) = await _apiService.GoogleResolvePlaceAsync(description, country);
                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    NewAddress.AddressLine1 = formatted;
                    OnPropertyChanged(nameof(NewAddress));
                }
                if (lat != 0 || lng != 0)
                {
                    NewAddress.Latitude = lat.ToString();
                    NewAddress.Longitude = lng.ToString();
                }
            }
            catch { }
        }
    }
}
