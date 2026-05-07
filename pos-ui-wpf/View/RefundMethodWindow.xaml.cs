using System.Windows;

namespace POS_UI.View
{
	public partial class RefundMethodWindow : Window
	{
		public string SelectedChoice { get; private set; }
		public RefundMethodWindow()
		{
			InitializeComponent();
		}

		private void OnCard(object sender, RoutedEventArgs e)
		{
			SelectedChoice = "CARD";
			DialogResult = true;
			Close();
		}

		private void OnSkip(object sender, RoutedEventArgs e)
		{
			SelectedChoice = "SKIP";
			DialogResult = true;
			Close();
		}

		private void OnCancel(object sender, RoutedEventArgs e)
		{
			SelectedChoice = null;
			DialogResult = false;
			Close();
		}

		private void OnClose(object sender, RoutedEventArgs e)
		{
			SelectedChoice = null;
			DialogResult = false;
			Close();
		}
	}
}


