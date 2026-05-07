using System;
using System.IO;

namespace POS_UI.Services
{
    public static class PathService
    {
        private const string BaseFolderName = "ALLPOSDETAILS";
        private const string OrdersFolderName = "POS-Orders";
        private const string DraftsFolderName = "Drafts";

        private static bool _initialized = false;
        private static bool _initializing = false;
        private static readonly object _initLock = new object();

        public static string GetBaseFolderPath()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            return Path.Combine(desktopPath, BaseFolderName);
        }

        public static string GetFilePath(string fileName)
        {
            EnsureInitialized();
            return Path.Combine(GetBaseFolderPath(), fileName);
        }

        public static string GetFolderPath(string folderName)
        {
            EnsureInitialized();
            string folder = Path.Combine(GetBaseFolderPath(), folderName);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }

        public static string GetOrdersFolderPath()
        {
            return GetFolderPath(OrdersFolderName);
        }

        public static string GetDraftsFilePath()
        {
            string draftsFolder = GetFolderPath(DraftsFolderName);
            return Path.Combine(draftsFolder, "drafts.json");
        }

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized || _initializing) return;
                _initializing = true;
                try
                {
                    string baseFolder = GetBaseFolderPath();
                    if (!Directory.Exists(baseFolder))
                    {
                        Directory.CreateDirectory(baseFolder);
                    }

                    // Ensure known subfolders WITHOUT re-entering EnsureInitialized
                    string ordersFolder = Path.Combine(baseFolder, OrdersFolderName);
                    if (!Directory.Exists(ordersFolder))
                    {
                        Directory.CreateDirectory(ordersFolder);
                    }

                    string draftsFolder = Path.Combine(baseFolder, DraftsFolderName);
                    if (!Directory.Exists(draftsFolder))
                    {
                        Directory.CreateDirectory(draftsFolder);
                    }

                    TryMigrateExistingFiles(baseFolder);

                    _initialized = true;
                }
                finally
                {
                    _initializing = false;
                }
            }
        }

        private static void TryMigrateExistingFiles(string baseFolder)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                // Known files previously stored directly on Desktop
                TryMoveFile(Path.Combine(desktop, "printers.txt"), Path.Combine(baseFolder, "printers.txt"));
                TryMoveFile(Path.Combine(desktop, "cardmachine.txt"), Path.Combine(baseFolder, "cardmachine.txt"));
                TryMoveFile(Path.Combine(desktop, "cardmachine_users.txt"), Path.Combine(baseFolder, "cardmachine_users.txt"));
                TryMoveFile(Path.Combine(desktop, "settings.txt"), Path.Combine(baseFolder, "settings.txt"));

                // POS-Orders folder previously on Desktop
                string oldOrdersFolder = Path.Combine(desktop, OrdersFolderName);
                string newOrdersFolder = Path.Combine(baseFolder, OrdersFolderName);
                TryMoveDirectory(oldOrdersFolder, newOrdersFolder);

                // Drafts previously under LocalApplicationData\POS_Printer_Connect\drafts.json
                string oldDraftsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "POS_Printer_Connect", "drafts.json");
                string newDraftsFile = Path.Combine(baseFolder, DraftsFolderName, "drafts.json");
                TryMoveFile(oldDraftsFile, newDraftsFile);
            }
            catch
            {
                // Swallow migration errors silently to avoid blocking startup
            }
        }

        private static void TryMoveFile(string source, string destination)
        {
            try
            {
                if (!File.Exists(source)) return;

                string? destDir = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                if (!File.Exists(destination))
                {
                    File.Move(source, destination);
                }
            }
            catch
            {
                // Ignore individual file move failures
            }
        }

        private static void TryMoveDirectory(string sourceDir, string destinationDir)
        {
            try
            {
                if (!Directory.Exists(sourceDir)) return;

                if (!Directory.Exists(destinationDir))
                {
                    Directory.Move(sourceDir, destinationDir);
                    return;
                }

                // If destination exists, attempt to merge files (shallow copy)
                foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    var relative = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var destFile = Path.Combine(destinationDir, relative);
                    var destParent = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(destParent) && !Directory.Exists(destParent))
                    {
                        Directory.CreateDirectory(destParent);
                    }
                    if (!File.Exists(destFile))
                    {
                        File.Move(file, destFile);
                    }
                }
            }
            catch
            {
                // Ignore directory merge/move failures
            }
        }
    }
}


