using System.Windows;

namespace POS_UI.View
{
    public partial class ConfirmWindow : Window
    {
        public bool? Result { get; private set; }
        public ConfirmWindow()
        {
            InitializeComponent();
        }

        private void OnYes(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
            Close();
        }

        private void OnNo(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
            Close();
        }
    }
}


