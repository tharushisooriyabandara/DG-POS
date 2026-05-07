using System.Windows.Controls;
using POS_UI.ViewModels;

namespace POS_UI.View
{
    public partial class TaxInfoDialog : UserControl
    {
        public TaxInfoDialog()
        {
            InitializeComponent();
            
            var viewModel = new TaxInfoDialogViewModel();
            DataContext = viewModel;
        }
    }
}

