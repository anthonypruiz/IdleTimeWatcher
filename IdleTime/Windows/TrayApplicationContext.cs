using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IdleTimeWatcher.Windows;
using Microsoft.Extensions.Hosting;

namespace IdleTimeWatcher;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly IHost _host;
    private readonly IdleTimeDetector _detector;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _idleItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly IntPtr _iconHandle;

    public TrayApplicationContext(IHost host, IdleTimeDetector detector, IHostApplicationLifetime lifetime)
    {
        _host = host;
        _detector = detector;

        _idleItem = new ToolStripMenuItem("Idle: --") { Enabled = false };

        _startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = StartupManager.IsInstalled(),
            CheckOnClick = true
        };
        _startupItem.Click += OnStartupToggled;

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += OnExit;

        var headerItem = new ToolStripMenuItem("IdleTimeWatcher")
        {
            Enabled = false,
            Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold)
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(headerItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_idleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        (_iconHandle, var icon) = CreateTrayIcon();
        _trayIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "IdleTimeWatcher",
            ContextMenuStrip = menu,
            Visible = true
        };

        // If the host shuts down externally (e.g. fatal error), exit the message loop too.
        lifetime.ApplicationStopping.Register(() => Application.Exit());

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var idle = _detector.GetIdleTime();
        var text = idle.TotalSeconds < 60
            ? $"Idle: {(int)idle.TotalSeconds}s"
            : $"Idle: {idle.TotalMinutes:F1}m";

        _idleItem.Text = text;

        // NotifyIcon tooltip has a 64-character hard limit.
        var tooltip = $"IdleTimeWatcher — {text}";
        _trayIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
    }

    private void OnStartupToggled(object? sender, EventArgs e)
    {
        try
        {
            if (_startupItem.Checked)
                StartupManager.Install();
            else
                StartupManager.Uninstall();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not update startup setting:\n{ex.Message}",
                "IdleTimeWatcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _startupItem.Checked = !_startupItem.Checked; // revert the visual state
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        Application.Exit();
    }

    private static (IntPtr handle, Icon icon) CreateTrayIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Blue circle background
            using var bgBrush = new SolidBrush(Color.FromArgb(0, 120, 215));
            g.FillEllipse(bgBrush, 1, 1, 13, 13);

            // Clock hands — visually suggests "time"
            using var pen = new Pen(Color.White, 1.5f);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawLine(pen, 7.5f, 7.5f, 7.5f, 4f);  // 12-o'clock hand
            g.DrawLine(pen, 7.5f, 7.5f, 11f, 7.5f); // 3-o'clock hand
        }

        var handle = bmp.GetHicon();
        // Icon.FromHandle doesn't own the handle; Clone() gives us an owned copy.
        using var temp = Icon.FromHandle(handle);
        return (handle, (Icon)temp.Clone());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            if (_iconHandle != IntPtr.Zero)
                DestroyIcon(_iconHandle);
        }
        base.Dispose(disposing);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
