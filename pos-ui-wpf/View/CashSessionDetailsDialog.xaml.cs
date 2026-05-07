using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS_UI.Models;
using POS_UI.ViewModels;

namespace POS_UI.View
{
    public partial class CashSessionDetailsDialog : UserControl
    {
        public CashSessionDetailsDialog()
        {
            InitializeComponent();
            Loaded += (s, e) => ConfigureScrollViewerForTouch(ReportScrollViewer);
        }

        public CashSessionDetailsDialog(CashDrawerSessionModel session) : this()
        {
            DataContext = new CashSessionDetailsDialogViewModel(session);
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
                System.Diagnostics.Debug.WriteLine($"[Touch] CashSessionDetails scroll setup error: {ex.Message}");
            }
        }
    }
}
