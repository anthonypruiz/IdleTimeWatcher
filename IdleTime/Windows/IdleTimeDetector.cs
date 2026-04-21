using System.Runtime.InteropServices;

namespace IdleTimeWatcher.Windows;

/// <summary>
/// Queries Windows for the time elapsed since the last keyboard or mouse input.
/// Must run in the interactive user session — GetLastInputInfo is not available to Session 0 services.
/// </summary>
internal sealed class IdleTimeDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };

        if (!GetLastInputInfo(ref info))
            throw new InvalidOperationException(
                $"GetLastInputInfo failed with error {Marshal.GetLastWin32Error()}. " +
                "The process must run in the interactive user session.");

        // Environment.TickCount wraps every ~49 days; unchecked subtraction handles the rollover correctly.
        uint idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }
}
