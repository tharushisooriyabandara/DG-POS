using System.ComponentModel;
using System.Windows.Input;
using POS_UI.Models;
using MaterialDesignThemes.Wpf;

namespace POS_UI.ViewModels
{
    public class PaymentConfirmationDialogViewModel : BaseViewModel
    {
        private OrderModel _order;

        public OrderModel Order
        {
            get => _order;
            set
            {
                _order = value;
                OnPropertyChanged(nameof(Order));
            }
        }

        public ICommand YesCommand { get; }
        public ICommand NoCommand { get; }

        public PaymentConfirmationDialogViewModel()
        {
            YesCommand = new RelayCommand(Yes);
            NoCommand = new RelayCommand(No);
        }

        private void Yes()
        {
            // Close dialog with "Yes" result
            MaterialDesignThemes.Wpf.DialogHost.Close("RootDialog", "Yes");
        }

        private void No()
        {
            // Close dialog with "No" result
            MaterialDesignThemes.Wpf.DialogHost.Close("RootDialog", "No");
        }
    }
}
