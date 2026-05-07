using System;
using System.IO;
using System.Linq;

namespace POS_UI.Services
{
    public static class EncryptionKeyService
    {
        public static byte[] GetOrLoadEncryptionKey()
        {
            var cfg = EnvironmentService.Instance.Config.Security;
            var credName = cfg.EncryptionKeyCredentialName;

            // 1) Try Credential Manager
            if (CredentialManagerService.TryGetSecret(credName, out var stored) && !string.IsNullOrWhiteSpace(stored))
            {
                try { return Convert.FromBase64String(stored); } catch { }
            }

            // 2) First-run: Require USB key file
            var key = LoadFromUsbKeyFile(cfg.UsbKeyFileName);
            if (key == null || key.Length != 32)
            {
                throw new Exception("Encryption key not found. Insert USB with keyfile.txt or set credential.");
            }

            // Save to Credential Manager for subsequent runs
            try
            {
                CredentialManagerService.SetSecret(credName, Convert.ToBase64String(key));
            }
            catch { }

            return key;
        }

        // Overload to obtain key before configuration binding using default or provided names
        public static byte[] GetOrLoadEncryptionKey(string usbKeyFileName, string credentialName)
        {
            // Try credential first
            if (CredentialManagerService.TryGetSecret(credentialName, out var stored) && !string.IsNullOrWhiteSpace(stored))
            {
                try { return Convert.FromBase64String(stored); } catch { }
            }
            // Fallback: USB
            var key = LoadFromUsbKeyFile(usbKeyFileName);
            if (key == null || key.Length != 32)
            {
                throw new Exception("Encryption key not found. Insert USB with keyfile.txt or set credential.");
            }
            // Persist
            try { CredentialManagerService.SetSecret(credentialName, Convert.ToBase64String(key)); } catch { }
            return key;
        }

        public static bool RequireUsbThisRun()
        {
            return EnvironmentService.Instance.Config.Security.RequireUsbKeyOnEveryRun;
        }

        public static byte[] LoadFromUsbKeyFile(string fileName)
        {
            try
            {
                // Search fixed and removable drives; prioritize removable
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed)
                    .OrderByDescending(d => d.DriveType == DriveType.Removable)
                    .ToList();

                foreach (var d in drives)
                {
                    try
                    {
                        if (!d.IsReady) continue;
                        var path = Path.Combine(d.RootDirectory.FullName, fileName);
                        if (File.Exists(path))
                        {
                            var text = File.ReadAllText(path).Trim();
                            var bytes = Convert.FromBase64String(text);
                            if (bytes.Length == 32) return bytes;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return Array.Empty<byte>();
        }
    }
}


