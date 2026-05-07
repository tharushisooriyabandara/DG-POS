using System;
using System.Windows;
using System.Windows.Controls;

namespace POS_UI.View
{
    /// <summary>
    /// Base class for optimized dialog performance
    /// Inherit from this for faster dialog opening and smoother interactions
    /// </summary>
    public class OptimizedDialogBase : UserControl
    {
        private bool _isOptimized = false;

        public OptimizedDialogBase()
        {
            // Optimize on load
            this.Loaded += OnDialogLoaded;
            this.Unloaded += OnDialogUnloaded;

            // Enable layout rounding for crisp rendering
            this.UseLayoutRounding = true;
            this.SnapsToDevicePixels = true;
        }

        private void OnDialogLoaded(object sender, RoutedEventArgs e)
        {
            if (_isOptimized) return;

            try
            {
                // Apply performance optimizations
                POS_UI.Helpers.PerformanceHelper.OptimizeControl(this);

                // Optimize any child ItemsControls (ListBox, ListView, DataGrid, etc.)
                OptimizeChildControls(this);

                _isOptimized = true;
            }
            catch { }
        }

        private void OnDialogUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clean up optimizations
                POS_UI.Helpers.PerformanceHelper.RemoveOptimization(this);
                _isOptimized = false;
            }
            catch { }
        }

        private void OptimizeChildControls(DependencyObject parent)
        {
            if (parent == null) return;

            try
            {
                int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < childCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                    // Optimize ItemsControls for virtualization
                    if (child is ItemsControl itemsControl)
                    {
                        POS_UI.Helpers.PerformanceHelper.OptimizeItemsControl(itemsControl);
                    }

                    // Optimize ScrollViewers
                    if (child is ScrollViewer scrollViewer)
                    {
                        POS_UI.Helpers.PerformanceHelper.OptimizeScrollViewer(scrollViewer);
                    }

                    // Recursively optimize children
                    OptimizeChildControls(child);
                }
            }
            catch { }
        }
    }
}

