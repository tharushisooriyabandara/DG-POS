using System;
using System.Windows.Input;

namespace POS_UI.ViewModels
{
	public class DateRangeDialogViewModel : BaseViewModel
	{
		private DateTime _fromDate;
		public DateTime FromDate { get => _fromDate; set { _fromDate = value; OnPropertyChanged(); } }

		private DateTime _toDate;
		public DateTime ToDate { get => _toDate; set { _toDate = value; OnPropertyChanged(); } }

		public string Title { get; set; } = "Select Date Range";

		public ICommand OkCommand { get; }
		public ICommand CancelCommand { get; }

		public DateRangeDialogViewModel()
		{
			OkCommand = new RelayCommand(() => Close(true));
			CancelCommand = new RelayCommand(() => Close(false));
		}

		private void Close(bool result)
		{
			MaterialDesignThemes.Wpf.DialogHost.Close("RootDialog", result);
		}
	}
}


