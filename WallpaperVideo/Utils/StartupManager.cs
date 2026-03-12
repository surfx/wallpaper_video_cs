using Microsoft.Win32;
using System.Reflection;

namespace WallpaperVideo.Utils
{
    public static class StartupManager
    {
        private const string KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "WallpaperVideo";

        public static bool IsStartupEnabled
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyPath, false);
                return key?.GetValue(AppName) != null;
            }
        }

        public static void SetStartup(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true);
            if (key == null) return;

            if (enable)
            {
                var processPath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(processPath))
                {
                    key.SetValue(AppName, processPath);
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
    }
}
