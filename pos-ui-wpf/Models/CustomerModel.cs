using System;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS_UI.Models
{
    public class CustomerModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string CountryCode { get; set; }
        public int CustomerId { get; set; }
        public string Address { get; set; }
        public List<CustomerAddressModel> Addresses { get; set; } = new List<CustomerAddressModel>();
        
        // Computed property that combines FirstName and LastName
        public string Name => $"{FirstName} {LastName}".Trim();
        
        // Computed property that combines CountryCode and Phone
        public string FullPhoneNumber 
        {
            get
            {
                var countryCode = string.IsNullOrWhiteSpace(CountryCode) ? "" : CountryCode.Trim();
                var phone = string.IsNullOrWhiteSpace(Phone) ? "" : Phone.Trim();
                
                if (string.IsNullOrWhiteSpace(countryCode) && string.IsNullOrWhiteSpace(phone))
                    return "";
                else if (string.IsNullOrWhiteSpace(countryCode))
                    return phone;
                else if (string.IsNullOrWhiteSpace(phone))
                    return countryCode;
                else
                {
                    // Remove the + from country code for comparison
                    var cleanCountryCode = countryCode.Replace("+", "");
                    
                    // Check if phone number starts with the country code digits
                    if (phone.StartsWith(cleanCountryCode))
                    {
                        // Remove the country code digits from the beginning of the phone number
                        var cleanPhone = phone.Substring(cleanCountryCode.Length);
                        return $"{countryCode} {cleanPhone}";
                    }
                    else
                    {
                        // If phone doesn't start with country code, display as is
                        return $"{countryCode} {phone}";
                    }
                }
            }
        }
        
        public string Initials
        {
            get
            {
                var fullName = ($"{FirstName} {LastName}").Trim();
                if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
                var letters = string.Join("", fullName
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n[0]))
                    .ToUpper();
                return letters.Length > 2 ? letters.Substring(0, 2) : letters;
            }
        }
    }

    public class CustomerDetailModel
    {
        public CustomerModel Customer { get; set; }
        public List<OrderModel> Orders { get; set; } = new List<OrderModel>();
    }

    public class CustomerAddressModel : INotifyPropertyChanged
    {
        private int _id;
        private string _label;
        private string _flatNo;
        private string _houseNo;
        private string _addressLine1;
        private string _addressLine2;
        private string _city;
        private string _landmark;
        private string _postalCode;
        private string _latitude;
        private string _longitude;
        private bool _isDefault;

        public int Id 
        { 
            get => _id; 
            set 
            { 
                _id = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public string Label 
        { 
            get => _label; 
            set 
            { 
                _label = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public string FlatNo 
        { 
            get => _flatNo; 
            set 
            { 
                _flatNo = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public string HouseNo 
        { 
            get => _houseNo; 
            set 
            { 
                _houseNo = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public string AddressLine1 
        { 
            get => _addressLine1; 
            set 
            { 
                _addressLine1 = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public string AddressLine2 
        { 
            get => _addressLine2; 
            set 
            { 
                _addressLine2 = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public string City 
        { 
            get => _city; 
            set 
            { 
                _city = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public string Landmark 
        { 
            get => _landmark; 
            set 
            { 
                _landmark = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public string PostalCode 
        { 
            get => _postalCode; 
            set 
            { 
                _postalCode = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public string Latitude 
        { 
            get => _latitude; 
            set 
            { 
                _latitude = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public string Longitude 
        { 
            get => _longitude; 
            set 
            { 
                _longitude = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public bool IsDefault 
        { 
            get => _isDefault; 
            set 
            { 
                _isDefault = value; 
                OnPropertyChanged(); 
            } 
        }

        public string FullAddress
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(FlatNo)) parts.Add(FlatNo.Trim());
                if (!string.IsNullOrWhiteSpace(HouseNo)) parts.Add(HouseNo.Trim());
                if (!string.IsNullOrWhiteSpace(AddressLine1)) parts.Add(AddressLine1.Trim());
                if (!string.IsNullOrWhiteSpace(AddressLine2)) parts.Add(AddressLine2.Trim());
                if (!string.IsNullOrWhiteSpace(Landmark)) parts.Add(Landmark.Trim());
                if (!string.IsNullOrWhiteSpace(City)) parts.Add(City.Trim());
                if (!string.IsNullOrWhiteSpace(PostalCode)) parts.Add(PostalCode.Trim());
                return string.Join(", ", parts);
            }
        }

        // Shows apartment/house number + address line 1 for concise display in dropdowns
        public string PrimaryAddressDisplay
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(FlatNo)) parts.Add(FlatNo.Trim());
                if (!string.IsNullOrWhiteSpace(HouseNo)) parts.Add(HouseNo.Trim());
                var prefix = string.Join(" ", parts);
                return string.IsNullOrWhiteSpace(prefix)
                    ? AddressLine1
                    : $"{prefix} {AddressLine1}".Trim();
            }
        }

        // Combined apartment/house number for simple displays
        public string ApartmentDisplay
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(FlatNo)) parts.Add(FlatNo.Trim());
                if (!string.IsNullOrWhiteSpace(HouseNo)) parts.Add(HouseNo.Trim());
                return string.Join(" ", parts);
            }
        }

        // For UI display in lists: shows "Add new address" for sentinel, else primary display
        public string DisplayLabel
            => Id == 0 && !string.IsNullOrWhiteSpace(Label)
                ? Label
                : (PrimaryAddressDisplay ?? string.Empty);

        public override string ToString()
        {
            var addressText = FullAddress;
            return string.IsNullOrWhiteSpace(Label) ? addressText : $"{Label} - {addressText}";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 