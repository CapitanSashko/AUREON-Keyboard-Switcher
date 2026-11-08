using Microsoft.Win32;

namespace AUREON_Keyboard_Switcher.Core
{
    public static class AutoStart
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "AUREON Keyboard Switcher";

        public static bool IsEnabled
        {
            get
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) is string;
            }
        }

        public static void Set(bool enabled)
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (enabled)
            {
                string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                key.SetValue(ValueName, $"\"{exePath}\" --tray");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
    }
}