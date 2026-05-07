using System.Windows; // For RoutedEventArgs, Visibility
using System.Windows.Controls; // For Page, ComboBox, ToggleButton
using System.Windows.Controls.Primitives; // For ToggleButton, Popup
using POS_UI.Models;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Windows.Input;
using POS_UI.ViewModels;
using System.Windows.Media;
namespace POS_UI.View
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
           
        
            
        }

private async void ToggleButton_Click(object sender, RoutedEventArgs e)
{
    if (sender is ToggleButton toggleButton && toggleButton.DataContext is PlatformModel platform)
    {
        var vm = DataContext as SettingsViewModel;
        
        // Get the current backend state
        bool currentBackendState = platform.IsActive;
        
        // Check if the toggle is currently ON (about to turn OFF)
        if (currentBackendState)
        {
            // Backend is ON, user wants to turn it OFF (snooze)
            // Show the snooze dialog - don't set IsUpdating here as it will be handled in the dialog
            vm?.OpenDialogCommand.Execute(platform);
        }
        else
        {
            // Backend is OFF, user wants to turn it ON (resume)
            // Set updating state and resume
            platform.IsUpdating = true;
            await vm?.ResumePlatformAsync(platform);
        }
    }
}

private void ViewShiftDetails_Click(object sender, RoutedEventArgs e)
{
    if (sender is Button btn && btn.DataContext is POS_UI.Models.UserModel user)
    {
        if (DataContext is POS_UI.ViewModels.SettingsViewModel vm)
        {
            vm.SelectedShiftUser = user;
            vm.ViewUserShiftDetailsCommand?.Execute(null);
        }
    }
}

// Ensure DatePicker opens calendar when clicking anywhere, without swallowing child clicks
private void DatePicker_FocusOpen(object sender, MouseButtonEventArgs e)
{
    if (sender is DatePicker dp)
    {
        if (!dp.IsDropDownOpen)
        {
            dp.IsDropDownOpen = true;
            dp.DisplayDate = dp.SelectedDate ?? System.DateTime.Today;
        }
        // Do NOT set e.Handled = true; it prevents date selection inside the calendar
    }
}

private void DatePicker_FocusOpen(object sender, TouchEventArgs e)
{
    if (sender is DatePicker dp)
    {
        if (!dp.IsDropDownOpen)
        {
            dp.IsDropDownOpen = true;
            dp.DisplayDate = dp.SelectedDate ?? System.DateTime.Today;
        }
        // Do NOT set e.Handled = true
    }
}

private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
{
    if (sender is DatePicker dp)
    {
        dp.IsDropDownOpen = false;
    }
}

private void DatePicker_CalendarClosed(object sender, RoutedEventArgs e)
{
    if (sender is DatePicker dp)
    {
        dp.IsDropDownOpen = false;
    }
}

private void DatePicker_Loaded(object sender, RoutedEventArgs e)
{
    if (sender is DatePicker dp)
    {
        // Ensure calendar renders current value
        dp.DisplayDate = dp.SelectedDate ?? System.DateTime.Today;
    }
}

private void DatePicker_GotFocus(object sender, RoutedEventArgs e)
{
    if (sender is DatePicker dp)
    {
        dp.IsDropDownOpen = true;
        dp.DisplayDate = dp.SelectedDate ?? System.DateTime.Today;
    }
}

    private async void ViewCashSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is CashDrawerSessionModel session)
        {
            var dialog = new CashSessionDetailsDialog(session);
            await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
        }
    }

    private bool _isDiscountDialogOpen;

    private async void DiscountPreset_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_isDiscountDialogOpen) return;
        var textBox = sender as TextBox;
        if (textBox == null) return;

        _isDiscountDialogOpen = true;

        // Move focus away immediately to prevent system keyboard
        Keyboard.ClearFocus();
        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(textBox), null);

        try
        {
            var tag = textBox.Tag?.ToString() ?? "";
            decimal? current = decimal.TryParse(textBox.Text, out var v) ? v : null;

            var vm = new ViewModels.NumericInputDialogViewModel($"Discount {tag}", current, 100, "%", "RootDialog");
            var dialog = new NumericInputDialog { DataContext = vm };
            var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");

            if (result is string valStr)
            {
                var settingsVm = DataContext as ViewModels.SettingsViewModel;
                if (settingsVm == null) return;
                switch (tag)
                {
                    case "1": settingsVm.DiscountPreset1 = valStr; break;
                    case "2": settingsVm.DiscountPreset2 = valStr; break;
                    case "3": settingsVm.DiscountPreset3 = valStr; break;
                    case "4": settingsVm.DiscountPreset4 = valStr; break;
                }
                settingsVm.SaveItemDiscountPresetsCommand.Execute(null);
            }
        }
        finally
        {
            Keyboard.ClearFocus();
            _isDiscountDialogOpen = false;
        }
    }

}} 