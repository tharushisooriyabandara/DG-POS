using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using POS_UI.Models;
using MaterialDesignThemes.Wpf;
using POS_UI.Services;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace POS_UI.ViewModels
{
    public class AddCustomerDialogViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        private string _customerName;
        public string CustomerName
        {
            get => _customerName;
            set { _customerName = value; OnPropertyChanged(); }
        }
        private string _customerPhone;
        public string CustomerPhone
        {
            get => _customerPhone;
            set { _customerPhone = value; OnPropertyChanged(); }
        }
        private string _countryCode;
        public string CountryCode
        {
            get => _countryCode;
            set { _countryCode = value; OnPropertyChanged(); }
        }

        private ObservableCollection<CountryCodeModel> _countryCodes;
        public ObservableCollection<CountryCodeModel> CountryCodes
        {
            get => _countryCodes;
            set { _countryCodes = value; OnPropertyChanged(); }
        }

        public ICollectionView CountryCodesView { get; private set; }

        private string _countrySearchText;
        public string CountrySearchText
        {
            get => _countrySearchText;
            set
            {
                _countrySearchText = value;
                OnPropertyChanged();
                CountryCodesView?.Refresh();
            }
        }

        private CountryCodeModel _selectedCountryCode;
        public CountryCodeModel SelectedCountryCode
        {
            get => _selectedCountryCode;
            set 
            { 
                _selectedCountryCode = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCountryCodeDisplay));
                if (value != null)
                {
                    CountryCode = value.DialCode;
                }
            }
        }

        public string SelectedCountryCodeDisplay
        {
            get => _selectedCountryCode?.DialCode ?? "";
        }
        public ICommand ProceedCommand { get; }
        public ICommand SkipCommand { get; }
        public ICommand CloseCommand { get; }

        public AddCustomerDialogViewModel()
        {
            _apiService = new ApiService();
            ProceedCommand = new RelayCommand(async () => await ProceedAsync(), CanProceed);
            SkipCommand = new RelayCommand(Skip);
            CloseCommand = new RelayCommand(Skip);
            
            // Initialize country codes first
            CountryCodes = new ObservableCollection<CountryCodeModel>();
            InitializeCountryCodes();
            CountryCodesView = CollectionViewSource.GetDefaultView(CountryCodes);
            if (CountryCodesView != null)
            {
                CountryCodesView.Filter = CountryFilter;
            }
            
            // Set default country code from shop details
            LoadDefaultCountryCode();
        }

        private void InitializeCountryCodes()
        {
            CountryCodes = new ObservableCollection<CountryCodeModel>
            {
                new CountryCodeModel { Code = "US", Name = "United States", DialCode = "+1" },
                new CountryCodeModel { Code = "CA", Name = "Canada", DialCode = "+1" },
                new CountryCodeModel { Code = "GB", Name = "United Kingdom", DialCode = "+44" },
                new CountryCodeModel { Code = "AU", Name = "Australia", DialCode = "+61" },
                new CountryCodeModel { Code = "DE", Name = "Germany", DialCode = "+49" },
                new CountryCodeModel { Code = "FR", Name = "France", DialCode = "+33" },
                new CountryCodeModel { Code = "IT", Name = "Italy", DialCode = "+39" },
                new CountryCodeModel { Code = "ES", Name = "Spain", DialCode = "+34" },
                new CountryCodeModel { Code = "NL", Name = "Netherlands", DialCode = "+31" },
                new CountryCodeModel { Code = "BE", Name = "Belgium", DialCode = "+32" },
                new CountryCodeModel { Code = "CH", Name = "Switzerland", DialCode = "+41" },
                new CountryCodeModel { Code = "AT", Name = "Austria", DialCode = "+43" },
                new CountryCodeModel { Code = "SE", Name = "Sweden", DialCode = "+46" },
                new CountryCodeModel { Code = "NO", Name = "Norway", DialCode = "+47" },
                new CountryCodeModel { Code = "DK", Name = "Denmark", DialCode = "+45" },
                new CountryCodeModel { Code = "FI", Name = "Finland", DialCode = "+358" },
                new CountryCodeModel { Code = "IE", Name = "Ireland", DialCode = "+353" },
                new CountryCodeModel { Code = "PT", Name = "Portugal", DialCode = "+351" },
                new CountryCodeModel { Code = "GR", Name = "Greece", DialCode = "+30" },
                new CountryCodeModel { Code = "PL", Name = "Poland", DialCode = "+48" },
                new CountryCodeModel { Code = "CZ", Name = "Czech Republic", DialCode = "+420" },
                new CountryCodeModel { Code = "HU", Name = "Hungary", DialCode = "+36" },
                new CountryCodeModel { Code = "RO", Name = "Romania", DialCode = "+40" },
                new CountryCodeModel { Code = "BG", Name = "Bulgaria", DialCode = "+359" },
                new CountryCodeModel { Code = "HR", Name = "Croatia", DialCode = "+385" },
                new CountryCodeModel { Code = "SI", Name = "Slovenia", DialCode = "+386" },
                new CountryCodeModel { Code = "SK", Name = "Slovakia", DialCode = "+421" },
                new CountryCodeModel { Code = "LT", Name = "Lithuania", DialCode = "+370" },
                new CountryCodeModel { Code = "LV", Name = "Latvia", DialCode = "+371" },
                new CountryCodeModel { Code = "EE", Name = "Estonia", DialCode = "+372" },
                new CountryCodeModel { Code = "MT", Name = "Malta", DialCode = "+356" },
                new CountryCodeModel { Code = "CY", Name = "Cyprus", DialCode = "+357" },
                new CountryCodeModel { Code = "LU", Name = "Luxembourg", DialCode = "+352" },
                new CountryCodeModel { Code = "IS", Name = "Iceland", DialCode = "+354" },
                new CountryCodeModel { Code = "LI", Name = "Liechtenstein", DialCode = "+423" },
                new CountryCodeModel { Code = "MC", Name = "Monaco", DialCode = "+377" },
                new CountryCodeModel { Code = "SM", Name = "San Marino", DialCode = "+378" },
                new CountryCodeModel { Code = "VA", Name = "Vatican City", DialCode = "+379" },
                new CountryCodeModel { Code = "AD", Name = "Andorra", DialCode = "+376" },
                new CountryCodeModel { Code = "IN", Name = "India", DialCode = "+91" },
                new CountryCodeModel { Code = "CN", Name = "China", DialCode = "+86" },
                new CountryCodeModel { Code = "JP", Name = "Japan", DialCode = "+81" },
                new CountryCodeModel { Code = "KR", Name = "South Korea", DialCode = "+82" },
                new CountryCodeModel { Code = "SG", Name = "Singapore", DialCode = "+65" },
                new CountryCodeModel { Code = "MY", Name = "Malaysia", DialCode = "+60" },
                new CountryCodeModel { Code = "TH", Name = "Thailand", DialCode = "+66" },
                new CountryCodeModel { Code = "VN", Name = "Vietnam", DialCode = "+84" },
                new CountryCodeModel { Code = "PH", Name = "Philippines", DialCode = "+63" },
                new CountryCodeModel { Code = "ID", Name = "Indonesia", DialCode = "+62" },
                new CountryCodeModel { Code = "BR", Name = "Brazil", DialCode = "+55" },
                new CountryCodeModel { Code = "MX", Name = "Mexico", DialCode = "+52" },
                new CountryCodeModel { Code = "AR", Name = "Argentina", DialCode = "+54" },
                new CountryCodeModel { Code = "CL", Name = "Chile", DialCode = "+56" },
                new CountryCodeModel { Code = "CO", Name = "Colombia", DialCode = "+57" },
                new CountryCodeModel { Code = "PE", Name = "Peru", DialCode = "+51" },
                new CountryCodeModel { Code = "VE", Name = "Venezuela", DialCode = "+58" },
                new CountryCodeModel { Code = "EC", Name = "Ecuador", DialCode = "+593" },
                new CountryCodeModel { Code = "BO", Name = "Bolivia", DialCode = "+591" },
                new CountryCodeModel { Code = "PY", Name = "Paraguay", DialCode = "+595" },
                new CountryCodeModel { Code = "UY", Name = "Uruguay", DialCode = "+598" },
                new CountryCodeModel { Code = "GY", Name = "Guyana", DialCode = "+592" },
                new CountryCodeModel { Code = "SR", Name = "Suriname", DialCode = "+597" },
                new CountryCodeModel { Code = "FK", Name = "Falkland Islands", DialCode = "+500" },
                new CountryCodeModel { Code = "ZA", Name = "South Africa", DialCode = "+27" },
                new CountryCodeModel { Code = "EG", Name = "Egypt", DialCode = "+20" },
                new CountryCodeModel { Code = "NG", Name = "Nigeria", DialCode = "+234" },
                new CountryCodeModel { Code = "KE", Name = "Kenya", DialCode = "+254" },
                new CountryCodeModel { Code = "GH", Name = "Ghana", DialCode = "+233" },
                new CountryCodeModel { Code = "ET", Name = "Ethiopia", DialCode = "+251" },
                new CountryCodeModel { Code = "TZ", Name = "Tanzania", DialCode = "+255" },
                new CountryCodeModel { Code = "UG", Name = "Uganda", DialCode = "+256" },
                new CountryCodeModel { Code = "ZM", Name = "Zambia", DialCode = "+260" },
                new CountryCodeModel { Code = "ZW", Name = "Zimbabwe", DialCode = "+263" },
                new CountryCodeModel { Code = "BW", Name = "Botswana", DialCode = "+267" },
                new CountryCodeModel { Code = "NA", Name = "Namibia", DialCode = "+264" },
                new CountryCodeModel { Code = "MZ", Name = "Mozambique", DialCode = "+258" },
                new CountryCodeModel { Code = "MG", Name = "Madagascar", DialCode = "+261" },
                new CountryCodeModel { Code = "MU", Name = "Mauritius", DialCode = "+230" },
                new CountryCodeModel { Code = "SC", Name = "Seychelles", DialCode = "+248" },
                new CountryCodeModel { Code = "KM", Name = "Comoros", DialCode = "+269" },
                new CountryCodeModel { Code = "DJ", Name = "Djibouti", DialCode = "+253" },
                new CountryCodeModel { Code = "SO", Name = "Somalia", DialCode = "+252" },
                new CountryCodeModel { Code = "SD", Name = "Sudan", DialCode = "+249" },
                new CountryCodeModel { Code = "SS", Name = "South Sudan", DialCode = "+211" },
                new CountryCodeModel { Code = "ER", Name = "Eritrea", DialCode = "+291" },
                new CountryCodeModel { Code = "LY", Name = "Libya", DialCode = "+218" },
                new CountryCodeModel { Code = "TN", Name = "Tunisia", DialCode = "+216" },
                new CountryCodeModel { Code = "DZ", Name = "Algeria", DialCode = "+213" },
                new CountryCodeModel { Code = "MA", Name = "Morocco", DialCode = "+212" },
                new CountryCodeModel { Code = "SN", Name = "Senegal", DialCode = "+221" },
                new CountryCodeModel { Code = "ML", Name = "Mali", DialCode = "+223" },
                new CountryCodeModel { Code = "BF", Name = "Burkina Faso", DialCode = "+226" },
                new CountryCodeModel { Code = "NE", Name = "Niger", DialCode = "+227" },
                new CountryCodeModel { Code = "TD", Name = "Chad", DialCode = "+235" },
                new CountryCodeModel { Code = "CF", Name = "Central African Republic", DialCode = "+236" },
                new CountryCodeModel { Code = "CM", Name = "Cameroon", DialCode = "+237" },
                new CountryCodeModel { Code = "GQ", Name = "Equatorial Guinea", DialCode = "+240" },
                new CountryCodeModel { Code = "GA", Name = "Gabon", DialCode = "+241" },
                new CountryCodeModel { Code = "CG", Name = "Republic of the Congo", DialCode = "+242" },
                new CountryCodeModel { Code = "CD", Name = "Democratic Republic of the Congo", DialCode = "+243" },
                new CountryCodeModel { Code = "AO", Name = "Angola", DialCode = "+244" },
                new CountryCodeModel { Code = "GW", Name = "Guinea-Bissau", DialCode = "+245" },
                new CountryCodeModel { Code = "CV", Name = "Cape Verde", DialCode = "+238" },
                new CountryCodeModel { Code = "GM", Name = "Gambia", DialCode = "+220" },
                new CountryCodeModel { Code = "GN", Name = "Guinea", DialCode = "+224" },
                new CountryCodeModel { Code = "SL", Name = "Sierra Leone", DialCode = "+232" },
                new CountryCodeModel { Code = "LR", Name = "Liberia", DialCode = "+231" },
                new CountryCodeModel { Code = "CI", Name = "Ivory Coast", DialCode = "+225" },
                new CountryCodeModel { Code = "TG", Name = "Togo", DialCode = "+228" },
                new CountryCodeModel { Code = "BJ", Name = "Benin", DialCode = "+229" },
                new CountryCodeModel { Code = "ST", Name = "São Tomé and Príncipe", DialCode = "+239" },
                new CountryCodeModel { Code = "RU", Name = "Russia", DialCode = "+7" },
                new CountryCodeModel { Code = "UA", Name = "Ukraine", DialCode = "+380" },
                new CountryCodeModel { Code = "BY", Name = "Belarus", DialCode = "+375" },
                new CountryCodeModel { Code = "MD", Name = "Moldova", DialCode = "+373" },
                new CountryCodeModel { Code = "GE", Name = "Georgia", DialCode = "+995" },
                new CountryCodeModel { Code = "AM", Name = "Armenia", DialCode = "+374" },
                new CountryCodeModel { Code = "AZ", Name = "Azerbaijan", DialCode = "+994" },
                new CountryCodeModel { Code = "KZ", Name = "Kazakhstan", DialCode = "+7" },
                new CountryCodeModel { Code = "UZ", Name = "Uzbekistan", DialCode = "+998" },
                new CountryCodeModel { Code = "KG", Name = "Kyrgyzstan", DialCode = "+996" },
                new CountryCodeModel { Code = "TJ", Name = "Tajikistan", DialCode = "+992" },
                new CountryCodeModel { Code = "TM", Name = "Turkmenistan", DialCode = "+993" },
                new CountryCodeModel { Code = "AF", Name = "Afghanistan", DialCode = "+93" },
                new CountryCodeModel { Code = "PK", Name = "Pakistan", DialCode = "+92" },
                new CountryCodeModel { Code = "BD", Name = "Bangladesh", DialCode = "+880" },
                new CountryCodeModel { Code = "LK", Name = "Sri Lanka", DialCode = "+94" },
                new CountryCodeModel { Code = "NP", Name = "Nepal", DialCode = "+977" },
                new CountryCodeModel { Code = "BT", Name = "Bhutan", DialCode = "+975" },
                new CountryCodeModel { Code = "MV", Name = "Maldives", DialCode = "+960" },
                new CountryCodeModel { Code = "MM", Name = "Myanmar", DialCode = "+95" },
                new CountryCodeModel { Code = "LA", Name = "Laos", DialCode = "+856" },
                new CountryCodeModel { Code = "KH", Name = "Cambodia", DialCode = "+855" },
                new CountryCodeModel { Code = "MN", Name = "Mongolia", DialCode = "+976" },
                new CountryCodeModel { Code = "TW", Name = "Taiwan", DialCode = "+886" },
                new CountryCodeModel { Code = "HK", Name = "Hong Kong", DialCode = "+852" },
                new CountryCodeModel { Code = "MO", Name = "Macau", DialCode = "+853" },
                new CountryCodeModel { Code = "TR", Name = "Turkey", DialCode = "+90" },
                new CountryCodeModel { Code = "IL", Name = "Israel", DialCode = "+972" },
                new CountryCodeModel { Code = "JO", Name = "Jordan", DialCode = "+962" },
                new CountryCodeModel { Code = "LB", Name = "Lebanon", DialCode = "+961" },
                new CountryCodeModel { Code = "SY", Name = "Syria", DialCode = "+963" },
                new CountryCodeModel { Code = "IQ", Name = "Iraq", DialCode = "+964" },
                new CountryCodeModel { Code = "IR", Name = "Iran", DialCode = "+98" },
                new CountryCodeModel { Code = "KW", Name = "Kuwait", DialCode = "+965" },
                new CountryCodeModel { Code = "SA", Name = "Saudi Arabia", DialCode = "+966" },
                new CountryCodeModel { Code = "AE", Name = "United Arab Emirates", DialCode = "+971" },
                new CountryCodeModel { Code = "QA", Name = "Qatar", DialCode = "+974" },
                new CountryCodeModel { Code = "BH", Name = "Bahrain", DialCode = "+973" },
                new CountryCodeModel { Code = "OM", Name = "Oman", DialCode = "+968" },
                new CountryCodeModel { Code = "YE", Name = "Yemen", DialCode = "+967" },
                new CountryCodeModel { Code = "PS", Name = "Palestine", DialCode = "+970" },
                new CountryCodeModel { Code = "CY", Name = "Cyprus", DialCode = "+357" },
                new CountryCodeModel { Code = "NZ", Name = "New Zealand", DialCode = "+64" },
                new CountryCodeModel { Code = "FJ", Name = "Fiji", DialCode = "+679" },
                new CountryCodeModel { Code = "PG", Name = "Papua New Guinea", DialCode = "+675" },
                new CountryCodeModel { Code = "SB", Name = "Solomon Islands", DialCode = "+677" },
                new CountryCodeModel { Code = "VU", Name = "Vanuatu", DialCode = "+678" },
                new CountryCodeModel { Code = "NC", Name = "New Caledonia", DialCode = "+687" },
                new CountryCodeModel { Code = "PF", Name = "French Polynesia", DialCode = "+689" },
                new CountryCodeModel { Code = "WS", Name = "Samoa", DialCode = "+685" },
                new CountryCodeModel { Code = "TO", Name = "Tonga", DialCode = "+676" },
                new CountryCodeModel { Code = "KI", Name = "Kiribati", DialCode = "+686" },
                new CountryCodeModel { Code = "TV", Name = "Tuvalu", DialCode = "+688" },
                new CountryCodeModel { Code = "NR", Name = "Nauru", DialCode = "+674" },
                new CountryCodeModel { Code = "PW", Name = "Palau", DialCode = "+680" },
                new CountryCodeModel { Code = "MH", Name = "Marshall Islands", DialCode = "+692" },
                new CountryCodeModel { Code = "FM", Name = "Micronesia", DialCode = "+691" },
                new CountryCodeModel { Code = "GU", Name = "Guam", DialCode = "+1" },
                new CountryCodeModel { Code = "MP", Name = "Northern Mariana Islands", DialCode = "+1" },
                new CountryCodeModel { Code = "AS", Name = "American Samoa", DialCode = "+1" },
                new CountryCodeModel { Code = "CK", Name = "Cook Islands", DialCode = "+682" },
                new CountryCodeModel { Code = "NU", Name = "Niue", DialCode = "+683" },
                new CountryCodeModel { Code = "TK", Name = "Tokelau", DialCode = "+690" },
                new CountryCodeModel { Code = "WF", Name = "Wallis and Futuna", DialCode = "+681" }
            };
        }

        private bool CountryFilter(object item)
        {
            if (item is CountryCodeModel country)
            {
                if (string.IsNullOrWhiteSpace(CountrySearchText)) return true;
                var term = CountrySearchText.Trim();
                return (country.Name?.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || (country.Code?.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || (country.DialCode?.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }
            return false;
        }

        private void LoadDefaultCountryCode()
        {
            try
            {
                // Ensure CountryCodes is initialized
                if (CountryCodes == null || CountryCodes.Count == 0)
                {
                    InitializeCountryCodes();
                }

                var shopDetails = GlobalDataService.Instance.ShopDetails;
                System.Diagnostics.Debug.WriteLine($"[AddCustomer] ShopDetails: {(shopDetails == null ? "<null>" : $"CountryCode='{shopDetails.CountryCode}'")}");
                
                if (shopDetails != null && !string.IsNullOrWhiteSpace(shopDetails.CountryCode))
                {
                    var shopCountryCode = shopDetails.CountryCode.Trim();
                    System.Diagnostics.Debug.WriteLine($"[AddCustomer] Shop country code: '{shopCountryCode}'");
                    
                    // Try to find matching country code - handle different possible formats
                    CountryCodeModel matchingCountry = null;
                    
                    // First try exact match with dial code
                    matchingCountry = CountryCodes?.FirstOrDefault(c => c.DialCode == shopCountryCode);
                    
                    // If not found, try matching by country code (2-letter ISO)
                    if (matchingCountry == null)
                    {
                        matchingCountry = CountryCodes?.FirstOrDefault(c => c.Code == shopCountryCode);
                    }
                    
                    // If not found, try matching by country name
                    if (matchingCountry == null)
                    {
                        matchingCountry = CountryCodes?.FirstOrDefault(c => 
                            c.Name.Equals(shopCountryCode, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    // If still not found, try matching dial code without the + sign
                    if (matchingCountry == null && shopCountryCode.StartsWith("+"))
                    {
                        var dialCodeWithoutPlus = shopCountryCode.Substring(1);
                        matchingCountry = CountryCodes?.FirstOrDefault(c => c.DialCode == "+" + dialCodeWithoutPlus);
                    }
                    
                    if (matchingCountry != null)
                    {
                        CountryCode = matchingCountry.DialCode;
                        SelectedCountryCode = matchingCountry;
                        System.Diagnostics.Debug.WriteLine($"[AddCustomer] Found matching country: {matchingCountry.Name} ({matchingCountry.DialCode})");
                    }
                    else
                    {
                        // No match found, use fallback
                        CountryCode = "+1";
                        var defaultCountry = CountryCodes?.FirstOrDefault(c => c.DialCode == "+1");
                        if (defaultCountry != null)
                        {
                            SelectedCountryCode = defaultCountry;
                        }
                        System.Diagnostics.Debug.WriteLine($"[AddCustomer] No matching country found for '{shopCountryCode}', using fallback: '+1'");
                    }
                }
                else
                {
                    // Fallback to a common default
                    CountryCode = "+1";
                    var defaultCountry = CountryCodes?.FirstOrDefault(c => c.DialCode == "+1");
                    if (defaultCountry != null)
                    {
                        SelectedCountryCode = defaultCountry;
                    }
                    System.Diagnostics.Debug.WriteLine($"[AddCustomer] No shop country code available, using fallback: '{CountryCode}'");
                }
            }
            catch (Exception ex)
            {
                // Fallback to a common default if shop details not available
                CountryCode = "+1";
                var defaultCountry = CountryCodes?.FirstOrDefault(c => c.DialCode == "+1");
                if (defaultCountry != null)
                {
                    SelectedCountryCode = defaultCountry;
                }
                System.Diagnostics.Debug.WriteLine($"[AddCustomer] Exception loading country code, using fallback: '{CountryCode}'. Error: {ex.Message}");
            }
        }

        private async Task ProceedAsync()
        {
            try
            {
                // Debug: Log the values being sent
                System.Diagnostics.Debug.WriteLine($"[AddCustomer] Creating customer with: Name='{CustomerName}', CountryCode='{CountryCode}', Phone='{CustomerPhone}'");
                
                // Client-side validation for phone number to provide user-friendly feedback before API call
                var inputPhone = CustomerPhone?.Trim() ?? string.Empty;
                var digitsOnly = new string(inputPhone.Where(char.IsDigit).ToArray());
                if (digitsOnly.Length != inputPhone.Length)
                {
                    var vmInvalid = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Invalid Format", "The phone number contains invalid characters. Please use only numbers (0-9).");
                    var dlgInvalid = new POS_UI.View.StatusDialog { DataContext = vmInvalid };
                     Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                    });
                    await Task.Delay(100);
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlgInvalid, "AddItemDialogHost");
                    return;
                }
                if (string.IsNullOrWhiteSpace(digitsOnly) || digitsOnly.Length < 9)
                {
                    var vmShort = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Invalid Phone Number", "The phone number you entered is too short. Please ensure it is at least 9 digits long.");
                    var dlgShort = new POS_UI.View.StatusDialog { DataContext = vmShort };
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                    });
                    await Task.Delay(100);
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlgShort, "AddItemDialogHost");
                    return;
                }
                
                
                // Create customer via API and close dialog returning the created customer
                var customer = await _apiService.CreateCustomerAsync(CustomerName, CountryCode, CustomerPhone);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Customer Created", $"Customer created successfully");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                // Close the current dialog first, then show the success message
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(customer, null);
                });
                
                // Wait a moment for the dialog to close, then show success message
                await Task.Delay(100);
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                //DialogHost.CloseDialogCommand.Execute(customer, null);
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
                
                //MessageBox.Show($"Failed to create customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    string header = jsonObject?.message ?? "Customer Creation Failed";
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
                        MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                    });

                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                }
                catch (JsonException)
                {
                    // If JSON parsing fails, fall back to showing the raw exception message
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Creating Customer", ex.Message);
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };

                    // Close the current dialog first, then show the error message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                    });

                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    await MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                }
            }
        }

        private bool CanProceed()
        {
            return !string.IsNullOrWhiteSpace(CustomerName) && !string.IsNullOrWhiteSpace(CustomerPhone);
        }

        private void Skip()
        {
            DialogHost.CloseDialogCommand.Execute(null, null);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 