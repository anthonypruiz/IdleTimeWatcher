namespace IdleTimeWatcher.Exporters;

internal interface IMetricExporter
{
    Task ExportAsync(TimeSpan idleTime, CancellationToken cancellationToken = default);
}
