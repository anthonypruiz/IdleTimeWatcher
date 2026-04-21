namespace IdleTimeWatcher.Models;

public sealed class WatcherOptions
{
    public int MinIntervalSeconds { get; set; } = 2;
    public int MaxIntervalSeconds { get; set; } = 10;
    public bool HideConsoleWindow { get; set; } = true;
    public bool ShowIdleTime { get; set; } = false;
}

public sealed class ZabbixOptions
{
    public bool Enabled { get; set; } = true;
    public string SenderPath { get; set; } = @"C:\zabbix\zabbix_sender.exe";
    public string ServerAddress { get; set; } = "192.168.101.233";
    public int ServerPort { get; set; } = 10051;
    public string ItemKey { get; set; } = "idletime";
}

public sealed class PrometheusRemoteWriteOptions
{
    public bool Enabled { get; set; } = false;
    public string Endpoint { get; set; } = "";
    public string JobLabel { get; set; } = "idle_time_watcher";
    public Dictionary<string, string> AdditionalLabels { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 10;
    public string BearerToken { get; set; } = "";
}
