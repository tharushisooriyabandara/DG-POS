using System.Windows;
using System.Windows.Input;

namespace POS_UI.View
{
	public partial class DateRangeWindow : Window
	{
		public DateRangeWindow()
		{
			InitializeComponent();
		}

		private void OnOk(object sender, RoutedEventArgs e)
		{
			if (DataContext is POS_UI.ViewModels.DateRangeDialogViewModel vm)
			{
				var from = vm.FromDate.Date;
				var to = vm.ToDate.Date;
				if (to < from)
				{
					System.Windows.MessageBox.Show("'To' date cannot be before 'From' date.", "Z Report", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}
				var daysInclusive = (to - from).TotalDays + 1; // inclusive day count
				if (daysInclusive > 31)
				{
					//System.Windows.MessageBox.Show("Please select below 32 days (maximum 31).", "Z Report", MessageBoxButton.OK, MessageBoxImage.Information);
					var vm1 = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Invalid Date Range", "Please select a date range of 31 days or less.");
					var dlg = new POS_UI.View.StatusDialog { DataContext = vm1 };
					MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
					return;
				}
			}
			DialogResult = true;
			Close();
		}

		private void OnCancel(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void OnClose(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void OnFromAreaMouseDown(object sender, MouseButtonEventArgs e)
		{
			FromPicker.IsDropDownOpen = true;
		}

		private void OnFromAreaTouchDown(object sender, TouchEventArgs e)
		{
			FromPicker.IsDropDownOpen = true;
		}

		private void OnToAreaMouseDown(object sender, MouseButtonEventArgs e)
		{
			ToPicker.IsDropDownOpen = true;
		}

		private void OnToAreaTouchDown(object sender, TouchEventArgs e)
		{
			ToPicker.IsDropDownOpen = true;
		}
	}
}


