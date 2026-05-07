using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POS_UI.View
{
    public partial class AddModifiersDialog : OptimizedDialogBase
    {
        public AddModifiersDialog()
        {
            InitializeComponent();
            Loaded += (s, e) => ConfigureScrollViewerForTouch(ModifiersScrollViewer);
            ModifiersScrollViewer.ScrollChanged += OnModifiersScrollChanged;
        }

        private void OnModifiersScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (ModifiersFadeOverlay == null) return;
            var sv = sender as ScrollViewer;
            if (sv == null) return;
            bool canScrollDown = sv.VerticalOffset + sv.ViewportHeight < sv.ExtentHeight - 1;
            ModifiersFadeOverlay.Opacity = canScrollDown ? 1 : 0;
        }

        private void ConfigureScrollViewerForTouch(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null) return;
            try
            {
                scrollViewer.CanContentScroll = false;
                scrollViewer.IsDeferredScrollingEnabled = false;

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
                System.Diagnostics.Debug.WriteLine($"[Touch] AddModifiersDialog scroll setup error: {ex.Message}");
            }
        }
    }
}
