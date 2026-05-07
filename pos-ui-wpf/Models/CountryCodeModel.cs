using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS_UI.Models
{
    public class CountryCodeModel : INotifyPropertyChanged
    {
        private string _code;
        private string _name;
        private string _dialCode;

        public string Code
        {
            get => _code;
            set { _code = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string DialCode
        {
            get => _dialCode;
            set { _dialCode = value; OnPropertyChanged(); }
        }

        public string DisplayText => $"{DialCode} ({Name})";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
