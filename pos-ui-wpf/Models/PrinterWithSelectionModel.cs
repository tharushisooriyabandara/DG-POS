using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS_UI.Models
{
    public class PrinterWithSelectionModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private PrinterModel _printer;

        public PrinterModel Printer
        {
            get => _printer;
            set
            {
                _printer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeviceName));
                OnPropertyChanged(nameof(ConnectedVia));
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public string DeviceName => Printer?.DeviceName;
        public string ConnectedVia => Printer?.ConnectedVia;
        public string Status => Printer?.Status;
        public string StatusColor => Printer?.StatusColor;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
