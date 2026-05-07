using System.Windows;

namespace POS_UI.View
{
    public partial class CancelReasonWindow : Window
    {
        public string SelectedReason { get; private set; }

        public CancelReasonWindow()
        {
            InitializeComponent();
        }

        private void OnProceed(object sender, RoutedEventArgs e)
        {
            if (DataContext is POS_UI.ViewModels.CancelReasonDialogViewModel vm)
            {
                SelectedReason = vm.SelectedReason;
                DialogResult = true;
                Close();
            }
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            SelectedReason = null;
            DialogResult = false;
            Close();
        }
    }
}

