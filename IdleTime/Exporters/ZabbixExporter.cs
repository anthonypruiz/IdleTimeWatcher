using System.Diagnostics;
using IdleTimeWatcher.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdleTimeWatcher.Exporters;

internal sealed class ZabbixExporter : IMetricExporter
{
    private readonly IOptionsMonitor<ZabbixOptions> _monitor;
    private readonly ILogger<ZabbixExporter> _logger;

    public ZabbixExporter(IOptionsMonitor<ZabbixOptions> monitor, ILogger<ZabbixExporter> logger)
    {
        _monitor = monitor;
        _logger = logger;
    }

    public async Task ExportAsync(TimeSpan idleTime, CancellationToken cancellationToken = default)
    {
        var opts = _monitor.CurrentValue;
        if (!opts.Enabled) return;

        var seconds = (long)idleTime.TotalSeconds;
        var args = $"-z {opts.ServerAddress} -p {opts.ServerPort} " +
                   $"-s \"{Environment.MachineName}\" -k {opts.ItemKey} -o {seconds}";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = opts.SenderPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogWarning("zabbix_sender exited {Code}: {Error}", process.ExitCode, stderr);
            }
            else
            {
                _logger.LogDebug("Zabbix: sent {Key}={Value}s to {Host}", opts.ItemKey, seconds, opts.ServerAddress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke zabbix_sender at {Path}", opts.SenderPath);
        }
    }
}
