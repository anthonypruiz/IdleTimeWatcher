# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IdleTimeWatcher is a **.NET 8.0 Windows** console application that monitors user keyboard/mouse idle time and exports it to **Prometheus Remote Write** and/or **Zabbix**. It must run in the logged-in user's interactive session (not Session 0) to access `GetLastInputInfo` from `user32.dll`.

## Build Commands

From the `IdleTime/` directory:

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Self-contained single-file publish
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output lands in `IdleTime/bin/Debug/net8.0-windows/` or the publish directory. Copy `IdleTimeWatcher.exe` and `appsettings.json` to the deployment folder.

## Architecture

```
Program.cs            Generic Host entry point; handles --install/--uninstall/--status CLI args
Worker.cs             BackgroundService — the main monitoring loop (2–10 s randomized interval)
Windows/
  IdleTimeDetector    P/Invoke GetLastInputInfo → returns TimeSpan
  ConsoleHider        P/Invoke GetConsoleWindow/ShowWindow
  StartupManager      HKCU\...\Run registry auto-start (--install/--uninstall)
Exporters/
  IMetricExporter     Interface: ExportAsync(TimeSpan, CancellationToken)
  ZabbixExporter      Shells out to zabbix_sender.exe
  PrometheusRemoteWriteExporter  HTTP POST snappy-compressed protobuf to remote_write endpoint
  ProtoEncoder        Hand-rolled Prometheus remote write 1.0 protobuf serializer (no codegen)
                      Types: ProtoLabel, ProtoSample, ProtoTimeSeries,
                             ProtoMetricMetadata, ProtoMetricType (GAUGE/COUNTER/…)
Models/AppOptions     WatcherOptions, ZabbixOptions, PrometheusRemoteWriteOptions
```

## Configuration

Settings live in `appsettings.json` (copied to output directory on build):

| Section | Key | Default | Purpose |
| --- | --- | --- | --- |
| `Logging` | `FilePath` | `C:\IdleTimeWatcher\logs\idle-.log` | Rolling daily log path |
| `Watcher` | `HideConsoleWindow` | `true` | Hide console on launch |
| `Watcher` | `MinIntervalSeconds` | `2` | Minimum send interval |
| `Watcher` | `MaxIntervalSeconds` | `10` | Maximum send interval |
| `Zabbix` | `Enabled` | `true` | Toggle Zabbix export |
| `Zabbix` | `SenderPath` | `C:\zabbix\zabbix_sender.exe` | Path to zabbix_sender |
| `Zabbix` | `ServerAddress` | `192.168.101.233` | Zabbix server IP |
| `Zabbix` | `ItemKey` | `idletime` | Zabbix item key (case-sensitive) |
| `PrometheusRemoteWrite` | `Enabled` | `false` | Toggle Prometheus export |
| `PrometheusRemoteWrite` | `Endpoint` | `""` | Remote write URL |
| `PrometheusRemoteWrite` | `BearerToken` | `""` | Auth token (optional) |
| `PrometheusRemoteWrite` | `AdditionalLabels` | `{}` | Extra metric labels |

## Auto-Start

The preferred deployment method is the HKCU registry Run key:

```bash
IdleTimeWatcher.exe --install    # register
IdleTimeWatcher.exe --uninstall  # deregister
IdleTimeWatcher.exe --status     # check
```

A Windows Scheduled Task with an "At logon" trigger (run as the logged-on user, NOT SYSTEM) is also supported — see README.md for full settings.

## Extending — Adding a New Exporter

1. Implement `IMetricExporter` in `Exporters/`.
2. Add an options class to `Models/AppOptions.cs`.
3. Register with `services.AddSingleton<IMetricExporter, YourExporter>()` in `Program.cs`.
4. Add the config section to `appsettings.json`.

`Worker` iterates all `IMetricExporter` registrations automatically.

## NuGet Packages

| Package | Purpose |
| --- | --- |
| `Microsoft.Extensions.Hosting` 8.x | Generic Host, DI, Options, Configuration |
| `Serilog` + sinks | Structured logging to console and rolling files |
| `Serilog.Extensions.Hosting` | Serilog integration with Generic Host |
| `Snappier` | Snappy compression for Prometheus remote write |

## Key Constraints

- **Must run as the logged-on user.** `GetLastInputInfo` returns 0 in Session 0 (Windows Services / SYSTEM).
- `Environment.MachineName` is used as the Zabbix host name and Prometheus `instance` label — it must match exactly in both systems.
- The Prometheus remote write protobuf encoder in `ProtoEncoder.cs` implements the remote_write 1.0 spec manually. Labels must be sorted lexicographically before sending.
- `ProtoMetricMetadata` (field 3 of `WriteRequest`) declares the metric as `ProtoMetricType.Gauge`. Do not remove this — without it Prometheus marks the metric type as `unknown`.

## Related Documentation

- `agents.md` — AI agent permissions, invariants, common task recipes, and verification checklist
- `README.md` — end-user installation, configuration, and integration guides
