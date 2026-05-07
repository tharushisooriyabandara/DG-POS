using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS_UI.ViewModels;

namespace POS_UI.View
{
    /// <summary>
    /// Dialog that displays X Report details (same data as printed in the X report).
    /// UI is similar to CashSessionDetailsDialog. Opens when user clicks the X Report button.
    /// Generate button invokes the callback set by the host (e.g. ReportsPage) to print via PrintXReportAsync.
    /// </summary>
    public partial class XReportDialog : UserControl
    {
        /// <summary>Callback invoked when the user clicks Generate (e.g. to print X report via PrintXReportAsync).</summary>
        public Func<System.Threading.Tasks.Task> OnGenerateRequested { get; set; }

        public XReportDialog()
        {
            InitializeComponent();
            DataContext = new XReportDialogViewModel();
            Loaded += (s, e) => ConfigureScrollViewerForTouch(ReportScrollViewer);
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (OnGenerateRequested == null) return;
            if (DataContext is XReportDialogViewModel vm)
                vm.IsPrinting = true;
            try
            {
                await OnGenerateRequested();
            }
            finally
            {
                if (DataContext is XReportDialogViewModel xvm)
                    xvm.IsPrinting = false;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
        }

        private void ConfigureScrollViewerForTouch(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null) return;
            try
            {
                Stylus.SetIsTapFeedbackEnabled(scrollViewer, false);
                Stylus.SetIsPressAndHoldEnabled(scrollViewer, false);
                Stylus.SetIsFlicksEnabled(scrollViewer, false);
                Stylus.SetIsTouchFeedbackEnabled(scrollViewer, false);

                bool isScrolling = false;
                Point? lastPosition = null;

                scrollViewer.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    lastPosition = e.GetPosition(scrollViewer);
                    isScrolling = false;
                };

                scrollViewer.PreviewMouseMove += (s, e) =>
                {
                    if (lastPosition.HasValue && e.LeftButton == MouseButtonState.Pressed)
                    {
                        var currentPosition = e.GetPosition(scrollViewer);
                        var delta = currentPosition.Y - lastPosition.Value.Y;

                        if (!isScrolling && Math.Abs(delta) > 20)
                        {
                            isScrolling = true;
                            scrollViewer.CaptureMouse();
                        }

                        if (isScrolling)
                        {
                            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - delta);
                            lastPosition = currentPosition;
                            e.Handled = true;
                        }
                    }
                };

                scrollViewer.PreviewMouseLeftButtonUp += (s, e) =>
                {
                    if (isScrolling)
                    {
                        scrollViewer.ReleaseMouseCapture();
                        e.Handled = true;
                    }
                    lastPosition = null;
                    isScrolling = false;
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Touch] XReport scroll setup error: {ex.Message}");
            }
        }
    }
}
