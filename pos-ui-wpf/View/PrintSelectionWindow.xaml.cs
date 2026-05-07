using System.Windows;

namespace POS_UI.View
{
    public partial class PrintSelectionWindow : Window
    {
        public PrintSelectionWindow()
        {
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try { DialogResult = false; } catch { }
            Close();
        }
    }
}


