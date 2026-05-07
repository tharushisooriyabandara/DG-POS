using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for CashDrawerAccessDialog.xaml
    /// </summary>
    public partial class CashDrawerAccessDialog : UserControl
    {
        public CashDrawerAccessDialog()
        {
            InitializeComponent();
            var viewModel = new ViewModels.CashDrawerAccessDialogViewModel();
            DataContext = viewModel;
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void UserComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Open dropdown when clicking anywhere on the control, but don't swallow events while open
            if (sender is ComboBox combo)
            {
                if (!combo.IsDropDownOpen)
                {
                    combo.IsDropDownOpen = true;
                    e.Handled = true;
                }
            }
        }

        private void UserComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is not ComboBox combo)
                return;

            // Delay to ensure popup visual tree is created
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    var popup = combo.Template?.FindName("PART_Popup", combo) as System.Windows.Controls.Primitives.Popup;
                    if (popup?.Child == null)
                        return;

                    // Let interactions inside the popup proceed unhindered
                    popup.PreviewMouseDown -= Popup_PreviewMouseDown;
                    popup.PreviewMouseDown += Popup_PreviewMouseDown;

                    var scrollViewer = FindVisualChild<ScrollViewer>(popup.Child);
                    if (scrollViewer == null)
                        return;

                    scrollViewer.Focusable = true;
                    scrollViewer.IsManipulationEnabled = true;
                    scrollViewer.PanningMode = PanningMode.VerticalOnly;
                    ScrollViewer.SetCanContentScroll(scrollViewer, true);
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                    scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

                    // Improve wheel scrolling reliability
                    scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
                    scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                }
                catch { /* ignore */ }
            }));
        }

        private void Popup_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Do not let page-level handlers mark popup interactions as handled
            e.Handled = false;
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                if (e.Delta < 0)
                    sv.LineDown();
                else
                    sv.LineUp();
                e.Handled = true;
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;
                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }
    }
}
