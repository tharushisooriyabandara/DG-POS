using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using POS_UI.ViewModels;

namespace POS_UI.View.Dialogs
{
    public partial class EditMenuTabDialog : UserControl
    {
        public EditMenuTabDialog()
        {
            InitializeComponent();
            this.Loaded += EditMenuTabDialog_Loaded;
        }

        private void EditMenuTabDialog_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigureScrollViewerForTouch(MenuGridScrollViewer);
            ConfigureScrollViewerForTouch(PickerCategoriesScrollViewer);
            ConfigureScrollViewerForTouch(PickerItemsScrollViewer);
        }

        /// <summary>
        /// Enables finger/touch scrolling on a ScrollViewer using the same
        /// PreviewMouse approach as the CashierHomePage.
        /// Touch input is promoted to mouse events, so this works for both.
        /// </summary>
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
                System.Diagnostics.Debug.WriteLine($"[Touch] Error configuring {scrollViewer.Name}: {ex.Message}");
            }
        }

        private void Slot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MenuGridSlot slot)
            {
                if (slot.IsEmpty)
                {
                    var vm = DataContext as EditMenuTabViewModel;
                    vm?.ActivateSlotCommand?.Execute(slot);
                }
            }
        }

        private void ColorCircle_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement colorElement && colorElement.DataContext is string color)
            {
                var slot = FindParentSlot(colorElement);
                if (slot != null)
                {
                    var vm = DataContext as EditMenuTabViewModel;
                    vm?.ChangeSlotColorCommand?.Execute(new object[] { slot, color });
                }
            }
            e.Handled = true;
        }

        private MenuGridSlot FindParentSlot(DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is MenuGridSlot slot)
                    return slot;

                if (current is System.Windows.Controls.Primitives.Popup popup)
                {
                    if (popup.DataContext is MenuGridSlot popupSlot)
                        return popupSlot;
                }

                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
