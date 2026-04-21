using System.Net.Http.Headers;
using IdleTimeWatcher.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Snappier;

namespace IdleTimeWatcher.Exporters;

/// <summary>
/// Pushes idle-time samples to a Prometheus Remote Write endpoint using the
/// remote_write 1.0 protocol (snappy-compressed protobuf over HTTPS).
/// Compatible with Prometheus 2.x, Grafana Mimir, Cortex, Thanos Receive, and VictoriaMetrics.
///
/// Uses IOptionsMonitor so changes to appsettings.json take effect on the next send
/// without restarting the process.
/// </summary>
internal sealed class PrometheusRemoteWriteExporter : IMetricExporter, IDisposable
{
    private readonly IOptionsMonitor<PrometheusRemoteWriteOptions> _monitor;
    private readonly ILogger<PrometheusRemoteWriteExporter> _logger;
    // HttpClient is kept as a long-lived singleton for connection pooling.
    // Per-request auth and timeout are applied on HttpRequestMessage / HttpClient.Timeout,
    // so they pick up config changes without recreating the client.
    private readonly HttpClient _http = new();

    public PrometheusRemoteWriteExporter(
        IOptionsMonitor<PrometheusRemoteWriteOptions> monitor,
        ILogger<PrometheusRemoteWriteExporter> logger)
    {
        _monitor = monitor;
        _logger = logger;
    }

    public async Task ExportAsync(TimeSpan idleTime, CancellationToken cancellationToken = default)
    {
        var opts = _monitor.CurrentValue;
        if (!opts.Enabled) return;

        // Apply timeout from current config — safe because this background service
        // never runs two ExportAsync calls concurrently.
        _http.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);

        var labels = BuildLabels(opts);
        var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var payload = ProtoEncoder.EncodeWriteRequest(
            timeSeries:
            [
                new ProtoTimeSeries(
                    Labels: labels,
                    Samples: [new ProtoSample(idleTime.TotalSeconds, timestampMs)])
            ],
            metadata:
            [
                new ProtoMetricMetadata(
                    MetricFamilyName: "idle_time_seconds",
                    Type: ProtoMetricType.Gauge,
                    Help: "Seconds since last keyboard or mouse input in the user session.",
                    Unit: "seconds")
            ]);

        var compressed = Snappy.CompressToArray(payload);

        using var content = new ByteArrayContent(compressed);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
        content.Headers.ContentEncoding.Add("snappy");

        using var request = new HttpRequestMessage(HttpMethod.Post, opts.Endpoint) { Content = content };
        request.Headers.Add("X-Prometheus-Remote-Write-Version", "0.1.0");

        // Auth token is read from current config on every request — no restart needed.
        if (!string.IsNullOrEmpty(opts.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.BearerToken);

        try
        {
            var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Remote write returned {Status}: {Body}", (int)response.StatusCode, body);
            }
            else
            {
                _logger.LogDebug("Prometheus: pushed {Value:F1}s idle to {Endpoint}", idleTime.TotalSeconds, opts.Endpoint);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send metric via Prometheus remote write to {Endpoint}", opts.Endpoint);
        }
    }

    private static List<ProtoLabel> BuildLabels(PrometheusRemoteWriteOptions opts)
    {
        var labels = new List<ProtoLabel>
        {
            new("__name__", "idle_time_seconds"),
            new("job",      opts.JobLabel),
            new("instance", Environment.MachineName),
        };

        foreach (var (k, v) in opts.AdditionalLabels)
            labels.Add(new ProtoLabel(k, v));

        // Prometheus requires labels to be sorted lexicographically by name.
        labels.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return labels;
    }

    public void Dispose() => _http.Dispose();
}
