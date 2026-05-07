using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using POS_UI.Models;
using System.Collections.ObjectModel;

namespace POS_UI.View
{
    public partial class SelectCustomerDialog : OptimizedDialogBase
    {
        public SelectCustomerDialog()
        {
            InitializeComponent();
            this.Loaded += SelectCustomerDialog_Loaded;
        }

        private void SelectCustomerDialog_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = FindChildScrollViewer(CustomerList);
            if (scrollViewer != null)
            {
                ConfigureScrollViewerForTouch(scrollViewer);
                scrollViewer.ScrollChanged += (s, ev) => UpdateEdgeFades(scrollViewer);
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() => UpdateEdgeFades(scrollViewer)));
            }
        }

        private void UpdateEdgeFades(ScrollViewer sv)
        {
            TopFade.Opacity = sv.VerticalOffset > 1 ? 1 : 0;
            BottomFade.Opacity = sv.VerticalOffset + sv.ViewportHeight < sv.ExtentHeight - 1 ? 1 : 0;
        }

        private void ConfigureScrollViewerForTouch(ScrollViewer scrollViewer)
        {
            try
            {
                Stylus.SetIsTapFeedbackEnabled(scrollViewer, false);
                Stylus.SetIsPressAndHoldEnabled(scrollViewer, false);
                Stylus.SetIsFlicksEnabled(scrollViewer, false);
                Stylus.SetIsTouchFeedbackEnabled(scrollViewer, false);

                bool isScrolling = false;
                Point? lastPosition = null;

                scrollViewer.PreviewMouseLeftButtonDown += (s, ev) =>
                {
                    lastPosition = ev.GetPosition(scrollViewer);
                    isScrolling = false;
                };

                scrollViewer.PreviewMouseMove += (s, ev) =>
                {
                    if (lastPosition.HasValue && ev.LeftButton == MouseButtonState.Pressed)
                    {
                        var currentPosition = ev.GetPosition(scrollViewer);
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
                            ev.Handled = true;
                        }
                    }
                };

                scrollViewer.PreviewMouseLeftButtonUp += (s, ev) =>
                {
                    if (isScrolling)
                    {
                        scrollViewer.ReleaseMouseCapture();
                        ev.Handled = true;
                    }
                    lastPosition = null;
                    isScrolling = false;
                };
            }
            catch { }
        }

        private static ScrollViewer FindChildScrollViewer(DependencyObject parent)
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer sv) return sv;
                var result = FindChildScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void OnDialogClosing(object sender, MaterialDesignThemes.Wpf.DialogClosingEventArgs eventArgs)
        {
            if (eventArgs.Parameter is CustomerModel newCustomer && newCustomer != null)
            {
                if (DataContext is POS_UI.ViewModels.SelectCustomerDialogViewModel vm)
                {
                    if (!vm.AllCustomers.Contains(newCustomer))
                        vm.AllCustomers.Add(newCustomer);
                    vm.FilteredCustomers = new ObservableCollection<CustomerModel>(vm.AllCustomers);
                    vm.SelectedCustomer = newCustomer;
                }
            }
        }

        private void AlphaIndex_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is TextBlock tb)
                {
                    string letter = tb.Text?.Trim();
                    if (string.IsNullOrWhiteSpace(letter)) return;

                    if (DataContext is POS_UI.ViewModels.SelectCustomerDialogViewModel vm)
                    {
                        var list = vm.FilteredCustomers;
                        if (list == null || list.Count == 0) return;

                        CustomerModel target = null;
                        if (letter == "#")
                        {
                            target = list.FirstOrDefault(c => string.IsNullOrEmpty(c?.Name) || !char.IsLetter(char.ToLowerInvariant(c.Name[0])));
                        }
                        else
                        {
                            char ch = char.ToLowerInvariant(letter[0]);
                            target = list.FirstOrDefault(c => !string.IsNullOrEmpty(c?.Name) && char.ToLowerInvariant(c.Name[0]) == ch);
                        }

                        if (target != null)
                        {
                            CustomerList.ScrollIntoView(target);
                            CustomerList.SelectedItem = target;
                        }
                    }
                }
            }
            catch { }
        }
    }
}
