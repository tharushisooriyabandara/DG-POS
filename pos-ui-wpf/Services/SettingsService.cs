using System;
using System.IO;

namespace POS_UI.Services
{
    public class SettingsService
    {
        private string SettingsFilePath => PathService.GetFilePath(EnvironmentService.Instance.Config.Files.SettingsFileName);

        public (string TenantCode, string OutletCode, string BrandId) LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var lines = File.ReadAllLines(SettingsFilePath);
                    string tenantCode="";
                    string outletCode="";
                    string brandId="";

                    foreach (var line in lines)
                    {
                        if (line.StartsWith("TenantCode="))
                        {
                            // IMPORTANT: Normalize to lowercase for Firebase collection name consistency
                            tenantCode = line.Replace("TenantCode=", "").Trim().ToLowerInvariant();
                        }
                        else if (line.StartsWith("OutletCode="))
                        {
                            // IMPORTANT: Normalize to lowercase for Firebase collection name consistency
                            outletCode = line.Replace("OutletCode=", "").Trim().ToLowerInvariant();
                        }
                        else if (line.StartsWith("BrandId="))
                        {
                            brandId = line.Replace("BrandId=", "").Trim();
                        }
                    }

                    return (tenantCode, outletCode, brandId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }

            // Return default values if file doesn't exist or there's an error
            //return ("subway", "", "");
            return ("", "", "");
        }

        public string LoadGoogleApiKey()
        {
            try
            {
                // Optional: allow override via environment variable
                var env = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();

                var isProduction = string.Equals(EnvironmentService.Instance.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase);
                if (isProduction)
                {
                    // Encrypted .enc next to executable
                    var encName = EnvironmentService.Instance.Config.Files.GoogleMapsApiKeyEncryptedFileName;
                    var encPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, encName);
                    if (File.Exists(encPath))
                    {
                        var keyBytes = EncryptionKeyService.GetOrLoadEncryptionKey();
                        var payload = File.ReadAllBytes(encPath);
                        var plain = EncryptionService.Decrypt(payload, keyBytes);
                        var str = System.Text.Encoding.UTF8.GetString(plain).Trim();
                        if (!string.IsNullOrWhiteSpace(str)) return str;
                    }
                }
                else
                {
                    // Development: plaintext file
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
                    var keyFile = Path.Combine(baseDir, EnvironmentService.Instance.Config.Files.GoogleMapsApiKeyFileName);
                    if (File.Exists(keyFile))
                    {
                        var key = File.ReadAllText(keyFile).Trim();
                        if (!string.IsNullOrWhiteSpace(key)) return key;
                    }

                    // Also check working directory (useful during development)
                    var cwdFile = Path.Combine(Directory.GetCurrentDirectory(), EnvironmentService.Instance.Config.Files.GoogleMapsApiKeyFileName);
                    if (File.Exists(cwdFile))
                    {
                        var key = File.ReadAllText(cwdFile).Trim();
                        if (!string.IsNullOrWhiteSpace(key)) return key;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Google API key: {ex.Message}");
            }
            return string.Empty;
        }

        public bool SettingsExist()
        {
            return File.Exists(SettingsFilePath);
        }
    }
} 