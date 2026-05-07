using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS_UI.Models;
using POS_UI.ViewModels;

namespace POS_UI.View.Dialogs
{
    public partial class EditFloorPlanDialog : UserControl
    {
        private FloorPlanTablePlacementModel? _draggingFloorTable;
        private Point _floorTableDragStartPoint;
        private bool _isDraggingFloorTable;
        private TouchDevice? _activeFloorTableTouch;

        public EditFloorPlanDialog()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            DataObject.AddPastingHandler(FloorPlanWidthBox, FloorPlanDimTextBox_OnPasting);
            DataObject.AddPastingHandler(FloorPlanHeightBox, FloorPlanDimTextBox_OnPasting);
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            DataObject.RemovePastingHandler(FloorPlanWidthBox, FloorPlanDimTextBox_OnPasting);
            DataObject.RemovePastingHandler(FloorPlanHeightBox, FloorPlanDimTextBox_OnPasting);
        }

        /// <summary>
        /// Scroll the customization panel from the tunneling phase so nested controls (e.g. shape ListBox)
        /// do not mark the wheel as handled and block vertical scrolling after a selection moves focus.
        /// </summary>
        private void CustomizePanelScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            var lines = SystemParameters.WheelScrollLines;
            if (lines < 1)
            {
                lines = 1;
            }

            for (var i = 0; i < lines; i++)
            {
                if (e.Delta > 0)
                {
                    scrollViewer.LineUp();
                }
                else
                {
                    scrollViewer.LineDown();
                }
            }

            e.Handled = true;
        }

        private void FloorPlanDimTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = string.IsNullOrEmpty(e.Text) || !e.Text.All(char.IsDigit);
        }

        private void FloorPlanDimTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.V || e.Key == Key.C || e.Key == Key.X || e.Key == Key.A)
                    return;
            }

            if (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Tab ||
                e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
                return;

            if ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
                return;

            e.Handled = true;
        }

        private void FloorPlanDimTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrEmpty(text) || !text.All(char.IsDigit))
                e.CancelCommand();
        }

        private void FloorPlanTable_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_activeFloorTableTouch != null)
                return;

            if (sender is not System.Windows.Controls.Border border || border.DataContext is not FloorPlanTablePlacementModel table)
                return;

            _draggingFloorTable = table;
            _floorTableDragStartPoint = e.GetPosition(this);
            _isDraggingFloorTable = true;
            border.CaptureMouse();

            if (DataContext is SettingsViewModel vm)
                vm.SelectPlacedFloorPlanTable(table);
        }

        private void FloorPlanTable_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingFloorTable || _draggingFloorTable == null || sender is not System.Windows.Controls.Border)
                return;

            var currentPoint = e.GetPosition(this);
            var deltaX = currentPoint.X - _floorTableDragStartPoint.X;
            var deltaY = currentPoint.Y - _floorTableDragStartPoint.Y;
            _floorTableDragStartPoint = currentPoint;

            if (DataContext is SettingsViewModel vm)
                vm.MoveSelectedFloorPlanTable(deltaX, deltaY);
        }

        private void FloorPlanTable_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
                border.ReleaseMouseCapture();

            _isDraggingFloorTable = false;
            _draggingFloorTable = null;
        }

        private void FloorPlanTable_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            if (sender is not Border border || border.DataContext is not FloorPlanTablePlacementModel table)
                return;

            border.CaptureTouch(e.TouchDevice);
            _activeFloorTableTouch = e.TouchDevice;
            _draggingFloorTable = table;
            _floorTableDragStartPoint = e.GetTouchPoint(this).Position;
            _isDraggingFloorTable = true;

            if (DataContext is SettingsViewModel vm)
                vm.SelectPlacedFloorPlanTable(table);

            e.Handled = true;
        }

        private void FloorPlanTable_TouchMove(object sender, TouchEventArgs e)
        {
            if (!_isDraggingFloorTable || _draggingFloorTable == null || sender is not Border)
                return;
            if (_activeFloorTableTouch != e.TouchDevice)
                return;

            var currentPoint = e.GetTouchPoint(this).Position;
            var deltaX = currentPoint.X - _floorTableDragStartPoint.X;
            var deltaY = currentPoint.Y - _floorTableDragStartPoint.Y;
            _floorTableDragStartPoint = currentPoint;

            if (DataContext is SettingsViewModel vm)
                vm.MoveSelectedFloorPlanTable(deltaX, deltaY);

            e.Handled = true;
        }

        private void FloorPlanTable_TouchUp(object sender, TouchEventArgs e)
        {
            if (sender is not Border border || _activeFloorTableTouch != e.TouchDevice)
                return;

            if (e.TouchDevice.Captured == border)
                border.ReleaseTouchCapture(e.TouchDevice);

            _activeFloorTableTouch = null;
            _isDraggingFloorTable = false;
            _draggingFloorTable = null;
            e.Handled = true;
        }

        private void FloorPlanTable_LostTouchCapture(object sender, TouchEventArgs e)
        {
            if (_activeFloorTableTouch != e.TouchDevice)
                return;

            _activeFloorTableTouch = null;
            _isDraggingFloorTable = false;
            _draggingFloorTable = null;
        }

        private void FloorPlanColorCircle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el || el.DataContext is not string color)
            {
                return;
            }

            if (DataContext is SettingsViewModel vm && vm.SelectedPlacedFloorPlanTable != null)
            {
                vm.SelectedPlacedFloorPlanTable.ColorHex = color;
                vm.IsFloorPlanColorPickerOpen = false;
            }

            e.Handled = true;
        }
    }
}
