using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace POS_UI.Services
{
    public sealed class EnvironmentService
    {
        private static readonly Lazy<EnvironmentService> _lazy = new Lazy<EnvironmentService>(() => new EnvironmentService());
        public static EnvironmentService Instance => _lazy.Value;

        private IConfigurationRoot _configuration;

        public EnvConfig Config { get; private set; } = new EnvConfig();
        public string EnvironmentName { get; private set; } = "Development";

        private EnvironmentService() { }

        public void Initialize()
        {
            // Determine environment from POS_ENV, DOTNET_ENVIRONMENT, or default to Development
            string defaultEnv = "Development";
#if DEBUG
            defaultEnv = "Development";
#else
            defaultEnv = "Production";
#endif

            var env = Environment.GetEnvironmentVariable("POS_ENV")
                      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? defaultEnv;
            EnvironmentName = env;

            var basePath = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath);

            var baseEnc = Path.Combine(basePath, "appsettings.enc");
            if (File.Exists(baseEnc))
            {
                var usbName = "keyfile.txt";
                var credName = "POS_UI/EncryptionKey";
                try
                {
                    var key = EncryptionKeyService.GetOrLoadEncryptionKey(usbName, credName);
                    string AddDecrypted(string encPath)
                    {
                        var payload = File.ReadAllBytes(encPath);
                        var jsonBytes = EncryptionService.Decrypt(payload, key);
                        var tempPath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileName(encPath)}-{Guid.NewGuid():N}.json");
                        File.WriteAllBytes(tempPath, jsonBytes);
                        return tempPath;
                    }

                    var tmp = AddDecrypted(baseEnc);
                    builder.AddJsonFile(tmp, optional: true, reloadOnChange: false);

                    // Load the single env-specific enc file (build only publishes the correct one)
                    foreach (var envEncFile in Directory.GetFiles(basePath, "appsettings.*.enc"))
                    {
                        var t = AddDecrypted(envEncFile);
                        builder.AddJsonFile(t, optional: true, reloadOnChange: false);
                    }
                }
                catch { }
            }
            else
            {
                builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                       .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false);
            }

            // Only allow environment variables with POS_ prefix to override (avoid accidental system-wide overrides)
            builder.AddEnvironmentVariables("POS_");

            _configuration = builder.Build();

            var cfg = new EnvConfig();
            _configuration.Bind(cfg);

            // Provide safe fallbacks if not present
            cfg.Urls ??= new EnvUrls();
            cfg.Files ??= new EnvFiles();
            cfg.Auth ??= new EnvAuth();
            cfg.Security ??= new EnvSecurity();

            // No implicit dev defaults; rely strictly on appsettings (and POS_ env overrides)
            // This avoids silently masking configuration with hardcoded dev values

            if (string.IsNullOrWhiteSpace(cfg.Files.SettingsFileName)) cfg.Files.SettingsFileName = "settings.txt";
            if (string.IsNullOrWhiteSpace(cfg.Files.GoogleMapsApiKeyFileName)) cfg.Files.GoogleMapsApiKeyFileName = "google-maps-api-key.txt";
            if (string.IsNullOrWhiteSpace(cfg.Files.FirebaseServiceAccountFileName)) cfg.Files.FirebaseServiceAccountFileName = "testdelivergate-firebase-adminsdk-key.json";

            if (!string.IsNullOrWhiteSpace(cfg.EnvironmentName))
                EnvironmentName = cfg.EnvironmentName;

            Config = cfg;

#if DEBUG
            try
            {
                var appJson = System.IO.Path.Combine(basePath, "appsettings.json");
                var envJson = System.IO.Path.Combine(basePath, $"appsettings.{EnvironmentName}.json");
                System.Diagnostics.Debug.WriteLine($"[Env] BasePath: {basePath}");
                System.Diagnostics.Debug.WriteLine($"[Env] Using: appsettings.json exists={System.IO.File.Exists(appJson)}, appsettings.{EnvironmentName}.json exists={System.IO.File.Exists(envJson)}");
                System.Diagnostics.Debug.WriteLine($"[Env] EnvironmentName: {EnvironmentName}");
                System.Diagnostics.Debug.WriteLine($"[Env] Urls: Go={cfg.Urls.GoApiBaseUrl}, User={cfg.Urls.UserApiBaseUrl}, Reporting={cfg.Urls.ReportingBaseUrl}, Platform={cfg.Urls.PlatformBaseUrl}, Admin={cfg.Urls.AdminBaseUrl}");
            }
            catch { }
#endif
        }
    }

    public class EnvConfig
    {
        public string EnvironmentName { get; set; } = "";
        public EnvUrls Urls { get; set; } = new EnvUrls();
        public EnvFiles Files { get; set; } = new EnvFiles();
        public EnvAuth Auth { get; set; } = new EnvAuth();
        public EnvSecurity Security { get; set; } = new EnvSecurity();
    }

    public class EnvUrls
    {
        public string GoApiBaseUrl { get; set; } = string.Empty;
        public string UserApiBaseUrl { get; set; } = string.Empty;
        public string ReportingBaseUrl { get; set; } = string.Empty;
        public string PlatformBaseUrl { get; set; } = string.Empty;
        public string AdminBaseUrl { get; set; } = string.Empty;
    }

    public class EnvFiles
    {
        public string SettingsFileName { get; set; } = string.Empty;
        public string GoogleMapsApiKeyFileName { get; set; } = string.Empty; // plaintext (dev)
        public string FirebaseServiceAccountFileName { get; set; } = string.Empty; // plaintext (dev)
        public string GoogleMapsApiKeyEncryptedFileName { get; set; } = "google-maps-api-key.enc"; // prod
        public string FirebaseServiceAccountEncryptedFileName { get; set; } = "firebase-adminsdk.enc"; // prod
    }

    public class EnvAuth
    {
        public string LaravelClientId { get; set; } = string.Empty;
        public string LaravelClientSecret { get; set; } = string.Empty;
    }

    public class EnvSecurity
    {
        public string UsbKeyFileName { get; set; } = "keyfile.txt";
        public bool RequireUsbKeyOnEveryRun { get; set; } = false;
        public string EncryptionKeyCredentialName { get; set; } = "POS_UI/EncryptionKey";
    }
}


