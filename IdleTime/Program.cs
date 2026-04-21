using IdleTimeWatcher;
using IdleTimeWatcher.Exporters;
using IdleTimeWatcher.Models;
using IdleTimeWatcher.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

// ── CLI helpers ──────────────────────────────────────────────────────────────
if (args is ["--install"])
{
    StartupManager.Install();
    return 0;
}
if (args is ["--uninstall"])
{
    StartupManager.Uninstall();
    return 0;
}
if (args is ["--status"])
{
    Console.WriteLine(StartupManager.IsInstalled()
        ? "Auto-start: ENABLED  (HKCU\\...\\Run\\IdleTimeWatcher)"
        : "Auto-start: DISABLED");
    return 0;
}

// ── Bootstrap logger (used until host configuration is loaded) ────────────────
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

            // Both exporters are registered against the same interface so Worker receives
            // an IEnumerable<IMetricExporter> containing all active exporters.
            services.AddSingleton<IMetricExporter, ZabbixExporter>();
            services.AddSingleton<IMetricExporter, PrometheusRemoteWriteExporter>();

            services.AddHostedService<Worker>();
        })
        .Build();

    // Hide the console window after the host is built but before it starts running,
    // so early log output (startup messages) is still visible during development.
    var watcherOpts = host.Services.GetRequiredService<IOptions<WatcherOptions>>().Value;
    if (watcherOpts.HideConsoleWindow)
        ConsoleHider.Hide();

    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
