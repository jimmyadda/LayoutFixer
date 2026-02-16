using Microsoft.Win32;

namespace LayoutFixer;

public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LayoutFixer";

    public static void SetRunAtStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                          ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);

            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? "";
                if (!string.IsNullOrWhiteSpace(exePath))
                    key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch { /* ignore */ }
    }
}
