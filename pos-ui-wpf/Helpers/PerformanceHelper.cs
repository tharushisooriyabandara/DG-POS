using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace POS_UI.Helpers
{
    /// <summary>
    /// Performance optimization helpers for WPF UI responsiveness
    /// </summary>
    public static class PerformanceHelper
    {
        /// <summary>
        /// Optimize control for better rendering performance
        /// Call this on heavyweight controls (dialogs, data grids, etc.)
        /// </summary>
        public static void OptimizeControl(FrameworkElement element)
        {
            if (element == null) return;

            try
            {
                // Enable bitmap caching for faster redraws (good for static content)
                element.CacheMode = new BitmapCache
                {
                    EnableClearType = true,
                    RenderAtScale = 1.0,
                    SnapsToDevicePixels = true
                };

                // Optimize text rendering
                TextOptions.SetTextFormattingMode(element, TextFormattingMode.Ideal);
                TextOptions.SetTextRenderingMode(element, TextRenderingMode.Auto);

                // Enable subpixel positioning for smoother text
                RenderOptions.SetClearTypeHint(element, ClearTypeHint.Enabled);
            }
            catch { /* Silently fail if optimization not supported */ }
        }

        /// <summary>
        /// Remove optimization from control (before disposing)
        /// </summary>
        public static void RemoveOptimization(FrameworkElement element)
        {
            if (element == null) return;
            
            try
            {
                element.CacheMode = null;
            }
            catch { }
        }

        /// <summary>
        /// Optimize ScrollViewer for virtualization and performance
        /// </summary>
        public static void OptimizeScrollViewer(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null) return;

            try
            {
                // Enable smooth scrolling but limit rendering
                scrollViewer.CanContentScroll = true;
                scrollViewer.IsDeferredScrollingEnabled = true;
            }
            catch { }
        }

        /// <summary>
        /// Optimize ItemsControl/ListBox for better virtualization
        /// </summary>
        public static void OptimizeItemsControl(ItemsControl itemsControl)
        {
            if (itemsControl == null) return;

            try
            {
                // Check if the ItemsControl has already been measured
                // If the ItemContainerGenerator has already generated containers, 
                // it's too late to change virtualization settings
                if (itemsControl.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                {
                    // Already measured - skip virtualization settings to avoid exception
                    return;
                }

                // Check if the ItemsHost panel already exists (another sign it's been measured)
                // Use reflection to safely check the ItemsHost property
                var itemsHostProperty = typeof(ItemsControl).GetProperty("ItemsHost", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (itemsHostProperty != null)
                {
                    var itemsHost = itemsHostProperty.GetValue(itemsControl);
                    if (itemsHost != null)
                    {
                        // Panel already created - skip to avoid exception
                        return;
                    }
                }

                // Safe to apply virtualization settings before measure
                VirtualizingPanel.SetIsVirtualizing(itemsControl, true);
                VirtualizingPanel.SetVirtualizationMode(itemsControl, VirtualizationMode.Recycling);
                VirtualizingPanel.SetCacheLength(itemsControl, new VirtualizationCacheLength(20));
                VirtualizingPanel.SetCacheLengthUnit(itemsControl, VirtualizationCacheLengthUnit.Item);
                
                // Improve scrolling performance
                VirtualizingPanel.SetScrollUnit(itemsControl, ScrollUnit.Pixel);
            }
            catch { /* Silently skip if optimization fails */ }
        }

        /// <summary>
        /// Pre-measure and arrange an element to speed up first display
        /// Call this before showing a dialog for faster opening
        /// </summary>
        public static void PreloadElement(FrameworkElement element)
        {
            if (element == null) return;

            try
            {
                // Force measure/arrange cycle before display
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                element.Arrange(new Rect(element.DesiredSize));
                element.UpdateLayout();
            }
            catch { }
        }

        /// <summary>
        /// Force UI update immediately (use sparingly, can cause performance issues if overused)
        /// </summary>
        public static void ForceUIUpdate()
        {
            try
            {
                Application.Current?.Dispatcher?.Invoke(
                    System.Windows.Threading.DispatcherPriority.Render,
                    new Action(() => { }));
            }
            catch { }
        }

        /// <summary>
        /// Defer low-priority work until UI is idle
        /// </summary>
        public static void DeferUntilIdle(Action action)
        {
            if (action == null) return;

            try
            {
                Application.Current?.Dispatcher?.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                    action);
            }
            catch { }
        }
    }
}

