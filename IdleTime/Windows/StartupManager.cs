using Microsoft.Win32;

namespace IdleTimeWatcher.Windows;

/// <summary>
/// Manages the HKCU auto-run registry entry so the watcher starts with the user's session
/// without needing a Scheduled Task. This is the simplest reliable user-space auto-start mechanism.
/// </summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "IdleTimeWatcher";

    public static void Install()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the current executable path.");

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException($"Registry key not found: HKCU\\{RunKeyPath}");

        key.SetValue(AppName, $"\"{exePath}\"");
        Console.WriteLine($"Auto-start registered: {exePath}");
        Console.WriteLine("IdleTimeWatcher will launch automatically at next login.");
    }

    public static void Uninstall()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException($"Registry key not found: HKCU\\{RunKeyPath}");

        key.DeleteValue(AppName, throwOnMissingValue: false);
        Console.WriteLine("Auto-start registration removed.");
    }

    public static bool IsInstalled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(AppName) is not null;
    }
}
