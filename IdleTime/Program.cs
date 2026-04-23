using System.Windows.Forms;
using IdleTimeWatcher;
using IdleTimeWatcher.Exporters;
using IdleTimeWatcher.Models;
using IdleTimeWatcher.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // ── CLI helpers ───────────────────────────────────────────────────────
        if (args is ["--install"])
        {
            StartupManager.Install();
            MessageBox.Show(
                "IdleTimeWatcher will start automatically at next login.",
                "IdleTimeWatcher — Auto-start Enabled",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        if (args is ["--uninstall"])
        {
            StartupManager.Uninstall();
            MessageBox.Show(
                "Auto-start registration removed.",
                "IdleTimeWatcher — Auto-start Disabled",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        if (args is ["--status"])
        {
            var msg = StartupManager.IsInstalled()
                ? "Auto-start: ENABLED\nHKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\\IdleTimeWatcher"
                : "Auto-start: DISABLED";
            MessageBox.Show(msg, "IdleTimeWatcher — Status",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        // ── Bootstrap logger ──────────────────────────────────────────────────
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog((ctx, _, config) => config
                    .ReadFrom.Configuration(ctx.Configuration)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.File(
                        path: ctx.Configuration["Logging:FilePath"] ?? @"C:\IdleTimeWatcher\logs\idle-.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
                .ConfigureServices((ctx, services) =>
                {
                    services.Configure<WatcherOptions>(ctx.Configuration.GetSection("Watcher"));
                    services.Configure<ZabbixOptions>(ctx.Configuration.GetSection("Zabbix"));
                    services.Configure<PrometheusRemoteWriteOptions>(ctx.Configuration.GetSection("PrometheusRemoteWrite"));

                    services.AddSingleton<IdleTimeDetector>();
                    services.AddSingleton<IMetricExporter, ZabbixExporter>();
                    services.AddSingleton<IMetricExporter, PrometheusRemoteWriteExporter>();

                    services.AddHostedService<Worker>();
                })
                .Build();

            await host.StartAsync();

            var tray = new TrayApplicationContext(
                host,
                host.Services.GetRequiredService<IdleTimeDetector>(),
                host.Services.GetRequiredService<IHostApplicationLifetime>());

            // Blocks on the WinForms message loop; returns when the user clicks Exit
            // or when the host signals ApplicationStopping.
            Application.Run(tray);

            await host.StopAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            MessageBox.Show($"Fatal error:\n{ex.Message}", "IdleTimeWatcher",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
