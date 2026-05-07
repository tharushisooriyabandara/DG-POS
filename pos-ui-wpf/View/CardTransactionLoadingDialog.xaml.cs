using System.Windows.Controls;
using POS_UI.ViewModels;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for CardTransactionLoadingDialog.xaml
    /// </summary>
    public partial class CardTransactionLoadingDialog : UserControl
    {
        public CardTransactionLoadingViewModel ViewModel { get; }

        public CardTransactionLoadingDialog()
        {
            InitializeComponent();
            ViewModel = new CardTransactionLoadingViewModel();
            DataContext = ViewModel;
        }

        public void UpdateStatus(string status, string progress = null)
        {
            ViewModel.UpdateStatus(status, progress);
        }
    }
} 