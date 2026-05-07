using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace POS_UI.View
{
    /// <summary>
    /// Dialog to capture the reason for opening the cash drawer, with Cancel and Open actions.
    /// </summary>
    public partial class CashdrawerOpenReasonDialog : UserControl, INotifyPropertyChanged
    {
        private bool _isOpenEnabled;

        public CashdrawerOpenReasonDialog()
        {
            InitializeComponent();
            DataContext = this;
            UpdateCanOpen();
            if (ReasonTextBox != null)
                ReasonTextBox.TextChanged += (s, e) => UpdateCanOpen();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// True when the user has entered a non-empty reason; Open button is enabled only then.
        /// </summary>
        public bool IsOpenEnabled
        {
            get => _isOpenEnabled;
            private set
            {
                if (_isOpenEnabled == value) return;
                _isOpenEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOpenEnabled)));
            }
        }

        /// <summary>
        /// The reason entered by the user (non-null when Open was clicked).
        /// </summary>
        public string Reason { get; private set; }

        private void UpdateCanOpen()
        {
            IsOpenEnabled = !string.IsNullOrWhiteSpace(ReasonTextBox?.Text);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var reason = ReasonTextBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reason))
                return;
            Reason = reason;
            DialogHost.CloseDialogCommand.Execute(Reason, null);
        }
    }
}
