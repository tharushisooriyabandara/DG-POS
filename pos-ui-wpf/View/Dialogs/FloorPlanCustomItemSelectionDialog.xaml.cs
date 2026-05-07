using System.Windows;
using System.Windows.Controls;

namespace POS_UI.View.Dialogs
{
    public partial class FloorPlanCustomItemSelectionDialog : OptimizedDialogBase
    {
        public FloorPlanCustomItemSelectionDialog()
        {
            InitializeComponent();
            Loaded += OnLoadedAfterOptimizations;
        }

        /// <summary>
        /// <see cref="OptimizedDialogBase"/> runs <c>PerformanceHelper.OptimizeScrollViewer</c>, which sets
        /// <see cref="ScrollViewer.CanContentScroll"/> to true and causes tile content (especially wrapped labels)
        /// to clip. Restore pixel scrolling for this dialog.
        /// </summary>
        private void OnLoadedAfterOptimizations(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoadedAfterOptimizations;
            if (TileScroll != null)
            {
                TileScroll.CanContentScroll = false;
                TileScroll.IsDeferredScrollingEnabled = false;
            }
        }
    }
}
