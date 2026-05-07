using System;
using System.IO;
using System.Windows;
using System.Collections.Generic;

namespace POS_UI
{
    public partial class SettingsDialog : Window
    {
        private const string SettingsFileName = "settings.txt";
        private string SettingsFilePath => POS_UI.Services.PathService.GetFilePath(SettingsFileName);

        public string TenantCode { get; private set; }
        public string OutletCode { get; private set; }
        public string BrandId { get; private set; }

        public SettingsDialog()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var lines = File.ReadAllLines(SettingsFilePath);
                    if (lines.Length >= 2)
                    {
                        // Display in lowercase to maintain consistency
                        TenantCodeTextBox.Text = lines[0].Replace("TenantCode=", "").ToLowerInvariant();
                        OutletCodeTextBox.Text = lines[1].Replace("OutletCode=", "").ToLowerInvariant();
                    }
                    if (lines.Length >= 3)
                    {
                        BrandIdTextBox.Text = lines[2].Replace("BrandId=", "");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // IMPORTANT: Convert to lowercase to ensure Firebase collection name consistency
                // Firebase collection name = {tenantCode}_{outletCode} must be lowercase
                TenantCode = TenantCodeTextBox.Text.Trim().ToLowerInvariant();
                OutletCode = OutletCodeTextBox.Text.Trim().ToLowerInvariant();
                BrandId = BrandIdTextBox.Text.Trim();

                var missingFields = new List<string>();
                if (string.IsNullOrWhiteSpace(TenantCode))
                    missingFields.Add("Tenant Code");
                if (string.IsNullOrWhiteSpace(OutletCode))
                    missingFields.Add("Outlet Code");
                if (string.IsNullOrWhiteSpace(BrandId))
                    missingFields.Add("Brand Id");

                if (missingFields.Count > 0)
                {
                    string errorMessage;
                    if (missingFields.Count == 3)
                    {
                        errorMessage = "All three configuration fields are empty. Please enter Tenant Code, Outlet Code, and Brand Id to proceed.";
                    }
                    else if (missingFields.Count == 2)
                    {
                        var missingFieldsText = string.Join(" and ", missingFields);
                        errorMessage = $"Two required fields are missing: {missingFieldsText}. Please complete all configuration fields.";
                    }
                    else
                    {
                        var missingField = missingFields[0];
                        errorMessage = $"The {missingField} field is empty. Please enter a valid {missingField} to continue.";
                    }
                    
                    MessageBox.Show(errorMessage, "Configuration Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Save to file
                var settingsContent = $"TenantCode={TenantCode}\nOutletCode={OutletCode}\nBrandId={BrandId}";
                File.WriteAllText(SettingsFilePath, settingsContent);

               // MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 