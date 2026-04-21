using IdleTimeWatcher.Exporters;
using IdleTimeWatcher.Models;
using IdleTimeWatcher.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdleTimeWatcher;

internal sealed class Worker : BackgroundService
{
    private readonly IdleTimeDetector _detector;
    private readonly IEnumerable<IMetricExporter> _exporters;
    private readonly IOptionsMonitor<WatcherOptions> _monitor;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IdleTimeDetector detector,
        IEnumerable<IMetricExporter> exporters,
        IOptionsMonitor<WatcherOptions> monitor,
        ILogger<Worker> logger)
    {
        _detector = detector;
        _exporters = exporters;
        _monitor = monitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IdleTimeWatcher v2 started on {Machine}", Environment.MachineName);

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _monitor.CurrentValue;
            try
            {
                var idleTime = _detector.GetIdleTime();
                if (opts.ShowIdleTime)
                    _logger.LogInformation("Idle: {Seconds:F1}s ({IdleTime:g})", idleTime.TotalSeconds, idleTime);
                else
                    _logger.LogDebug("Idle time: {IdleTime:g}", idleTime);

                foreach (var exporter in _exporters)
                    await exporter.ExportAsync(idleTime, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metric export cycle");
            }

            var delaySec = Random.Shared.Next(opts.MinIntervalSeconds, opts.MaxIntervalSeconds + 1);
            await Task.Delay(TimeSpan.FromSeconds(delaySec), stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("IdleTimeWatcher stopped");
    }
}
