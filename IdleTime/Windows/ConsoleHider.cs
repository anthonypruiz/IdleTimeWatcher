using System.Runtime.InteropServices;

namespace IdleTimeWatcher.Windows;

internal static class ConsoleHider
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;

    public static void Hide()
    {
        var hwnd = GetConsoleWindow();
        if (hwnd != IntPtr.Zero)
            ShowWindow(hwnd, SW_HIDE);
    }
}
