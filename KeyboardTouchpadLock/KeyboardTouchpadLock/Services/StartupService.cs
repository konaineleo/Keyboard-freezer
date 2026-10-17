using Microsoft.Win32;

namespace KeyboardTouchpadLock.Services;

/// <summary>
/// Registers/unregisters the app to launch at Windows sign-in via the
/// standard per-user Run key. No admin rights or installer required.
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeyboardTouchpadLock";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null) return;

        if (enabled)
        {
            var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            // Quote the path in case it contains spaces (e.g. "Program Files").
            key.SetValue(ValueName, $"\"{exePath}\"");
        }
        else
        {
            if (key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) != null;
    }
}
