using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS_UI.ViewModels
{
    public class PinBoxViewModel : INotifyPropertyChanged
    {
        private string _text;
        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 