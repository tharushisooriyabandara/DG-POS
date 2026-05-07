using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace POS_UI.ViewModels
{
    public class ConfirmDialogViewModel : BaseViewModel
    {
        private string _title = "Confirm";
        public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }

        private string _message;
        public string Message { get => _message; set { _message = value; OnPropertyChanged(); } }

        public ICommand YesCommand { get; }
        public ICommand NoCommand { get; }

        public ConfirmDialogViewModel()
        {
            YesCommand = new RelayCommand(() => DialogHost.Close("RootDialog", true));
            NoCommand = new RelayCommand(() => DialogHost.Close("RootDialog", false));
        }
    }
}


