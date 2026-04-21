# IdleTimeWatcher

> A Windows user-session idle-time monitor that ships metrics to **Prometheus Remote Write** and/or **Zabbix** using a modern .NET 8 Generic Host architecture.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-GPLv3-blue)](LICENSE)

---

## Table of Contents

- [Overview](#overview)
- [How It Works](#how-it-works)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Features](#features)
- [Project Structure](#project-structure)
- [Requirements](#requirements)
- [Building from Source](#building-from-source)
- [Configuration](#configuration)
- [Deployment & Auto-Start](#deployment--auto-start)
  - [Option A — Registry Run Key (Recommended)](#option-a--registry-run-key-recommended)
  - [Option B — Windows Scheduled Task](#option-b--windows-scheduled-task)
- [Prometheus Remote Write Integration](#prometheus-remote-write-integration)
  - [Compatible Backends](#compatible-backends)
  - [Grafana Dashboard](#grafana-dashboard)
- [Zabbix Integration](#zabbix-integration)
  - [Item Setup](#item-setup)
  - [Alerting Triggers](#alerting-triggers)
- [Development Guide](#development-guide)
  - [Adding a New Exporter](#adding-a-new-exporter)
  - [Running in Debug Mode](#running-in-debug-mode)
- [Why Not a Windows Service?](#why-not-a-windows-service)
- [License](#license)

---

## Overview

IdleTimeWatcher solves a practical problem: knowing whether a workstation is actively in use.
It P/Invokes `GetLastInputInfo` from `user32.dll` to measure keyboard and mouse idle time, then pushes that value (in seconds) to one or both of:

| Sink | Protocol |
| --- | --- |
| **Prometheus Remote Write** | HTTP POST with snappy-compressed protobuf (remote_write 1.0) |
| **Zabbix** | CLI invocation of `zabbix_sender.exe` |

The metric can drive Grafana dashboards, Zabbix alerting triggers, or any other system that reads from a Prometheus-compatible TSDB.

**Original use-case:** attendance monitoring in an office environment — management wanted to know when employees arrived at and left their workstations, without invasive software, and receive email alerts on login/logout events.

---

## How It Works

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│  Windows User Session                                                        │
│                                                                              │
│  ┌──────────────────┐   GetLastInputInfo   ┌────────────────────────────┐   │
│  │  Keyboard/Mouse  │ ──────────────────▶  │  IdleTimeDetector          │   │
│  │  (user32.dll)    │                      │  (P/Invoke wrapper)        │   │
│  └──────────────────┘                      └────────────┬───────────────┘   │
│                                                         │ TimeSpan           │
│                                            ┌────────────▼───────────────┐   │
│                                            │  Worker (BackgroundService) │   │
│                                            │  loop delay: 2–10 s jitter │   │
│                                            └──────────┬─────────┬───────┘   │
│                                                       │         │            │
│                              ┌────────────────────────▼─┐  ┌───▼─────────────────────┐ │
│                              │  ZabbixExporter           │  │  PrometheusRemoteWrite  │ │
│                              │  (zabbix_sender CLI)      │  │  Exporter               │ │
│                              └──────────┬────────────────┘  │  (snappy protobuf HTTP) │ │
│                                         │                    └─────────────┬───────────┘ │
└─────────────────────────────────────────┼──────────────────────────────────┼───────────┘
                                          │                                  │
                              ┌───────────▼──────────────┐  ┌───────────────▼──────────────┐
                              │  Zabbix Server            │  │  Prometheus / Mimir /         │
                              │  → Grafana (optional)     │  │  VictoriaMetrics / Cortex     │
                              │  → Email alerts           │  │  → Grafana                   │
                              └──────────────────────────┘  └──────────────────────────────┘
```

---

## Architecture

The application is built on the **.NET 8 Generic Host** pattern, the same backbone used by ASP.NET Core and Worker Services.

| Concern | Approach |
| --- | --- |
| Application lifetime | `IHost` / `BackgroundService` |
| Configuration | `appsettings.json` + Options pattern (`IOptions<T>`) |
| Logging | Serilog — structured output to console + rolling daily files |
| Dependency Injection | `Microsoft.Extensions.DependencyInjection` |
| Exporter extensibility | `IMetricExporter` interface — add new sinks without touching `Worker` |
| Prometheus wire format | Hand-rolled protobuf encoder + Snappier compression (no codegen step) |
| Auto-start | HKCU registry `Run` key managed via `--install` / `--uninstall` CLI |

### Why a custom protobuf encoder?

The Prometheus Remote Write 1.0 specification uses a small, fixed protobuf schema (`WriteRequest → TimeSeries → Label/Sample/MetricMetadata`). Rather than pulling in `Google.Protobuf` and a `.proto` build step, this project ships a self-contained encoder in [`ProtoEncoder.cs`](IdleTime/Exporters/ProtoEncoder.cs) that covers all four message types including `MetricMetadata` (used to declare `idle_time_seconds` as a `GAUGE`). This keeps the dependency footprint minimal while remaining fully spec-compliant and easy to audit.

---

## Tech Stack

| Component | Technology |
| --- | --- |
| Runtime | .NET 8.0 (Windows) |
| Host | `Microsoft.Extensions.Hosting` 8.x |
| Logging | Serilog 3.x + Console and File sinks |
| Snappy compression | Snappier 1.x |
| Windows APIs | P/Invoke (`user32.dll`, `kernel32.dll`, `Microsoft.Win32` registry) |
| Build | `dotnet build` / MSBuild (SDK-style project) |

---

## Features

- **Zero-overhead idle detection** — `GetLastInputInfo` is a single kernel call with no polling of input devices
- **Dual metric sink** — Prometheus Remote Write and Zabbix can be enabled independently or simultaneously
- **Prometheus Remote Write 1.0** — compatible with Prometheus, Grafana Mimir, Cortex, Thanos Receive, and VictoriaMetrics
- **Bearer token auth** — configurable for authenticated remote-write endpoints (e.g., Grafana Cloud)
- **Structured logging** — Serilog outputs JSON-friendly logs to console and rolling daily log files
- **Self-installing auto-start** — `--install` writes to the HKCU registry Run key; no Scheduled Task wizard required
- **Graceful shutdown** — `CancellationToken` propagated through the entire call stack; no `Thread.Abort`
- **Randomized send interval** — jitter between 2–10 s avoids thundering-herd on shared Zabbix/Prometheus infrastructure
- **Silent background operation** — console window hidden via `GetConsoleWindow` / `ShowWindow` on startup

---

## Project Structure

```text
IdleTimeWatcher/
├── IdleTime/
│   ├── IdleTime.csproj            SDK-style .NET 8.0-windows project
│   ├── appsettings.json           Runtime configuration
│   ├── Program.cs                 Generic Host setup + CLI entry point
│   ├── Worker.cs                  BackgroundService — the main monitoring loop
│   ├── Models/
│   │   └── AppOptions.cs          Strongly-typed configuration option classes
│   ├── Windows/
│   │   ├── IdleTimeDetector.cs    P/Invoke wrapper for GetLastInputInfo
│   │   ├── ConsoleHider.cs        P/Invoke wrapper for GetConsoleWindow/ShowWindow
│   │   └── StartupManager.cs      HKCU registry auto-start management
│   └── Exporters/
│       ├── IMetricExporter.cs     Exporter interface — implement to add new sinks
│       ├── ZabbixExporter.cs      Zabbix integration via zabbix_sender CLI
│       ├── ProtoEncoder.cs        Prometheus remote write protobuf + MetricMetadata encoder
│       └── PrometheusRemoteWriteExporter.cs  HTTP remote write client
├── agents.md                      AI agent guide (capabilities, invariants, common tasks)
├── CLAUDE.md                      Claude Code specific guidance
├── README.md                      This file
└── LICENSE                        GNU GPLv3
```

---

## Requirements

| Requirement | Notes |
| --- | --- |
| .NET 8 Runtime | [Download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) — not needed if publishing self-contained |
| Windows 10 / 11 | Must run in the user's interactive session |
| `zabbix_sender.exe` | Only required if `Zabbix:Enabled` is `true` |
| Prometheus / Mimir / etc. | Only required if `PrometheusRemoteWrite:Enabled` is `true` |

---

## Building from Source

```bash
# Clone the repository
git clone https://github.com/anthonypruiz/IdleTimeWatcher.git
cd IdleTimeWatcher/IdleTime

# Restore dependencies and build (Debug)
dotnet build

# Release build
dotnet build -c Release

# Publish as a self-contained single-file executable (no runtime required on target)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The self-contained publish produces a single `IdleTimeWatcher.exe` in `bin/Release/net8.0-windows/win-x64/publish/`. Copy that file and `appsettings.json` to the deployment directory.

---

## Configuration

All settings live in `appsettings.json` next to the executable. No registry editing or XML config files required.

```jsonc
{
  "Logging": {
    // Rolling daily log files; the dash is replaced by the date stamp (e.g., idle-20240101.log).
    "FilePath": "C:\\IdleTimeWatcher\\logs\\idle-.log"
  },
  "Watcher": {
    "MinIntervalSeconds": 2,      // Minimum randomized send interval
    "MaxIntervalSeconds": 10,     // Maximum randomized send interval
    "HideConsoleWindow": true     // false = keep console visible (useful during development)
  },
  "Zabbix": {
    "Enabled": true,
    "SenderPath": "C:\\zabbix\\zabbix_sender.exe",
    "ServerAddress": "192.168.101.233",
    "ServerPort": 10051,
    "ItemKey": "idletime"         // Must match the Zabbix item key exactly (case-sensitive)
  },
  "PrometheusRemoteWrite": {
    "Enabled": false,
    "Endpoint": "http://prometheus:9090/api/v1/write",
    "JobLabel": "idle_time_watcher",
    "AdditionalLabels": {
      "environment": "production" // Arbitrary key/value labels attached to every sample
    },
    "TimeoutSeconds": 10,
    "BearerToken": ""             // Leave empty if the endpoint has no authentication
  }
}
```

To override the minimum log level, add a `Serilog` section:

```jsonc
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": { "Microsoft": "Warning" }
  }
}
```

---

## Deployment & Auto-Start

### Option A — Registry Run Key (Recommended)

The simplest and most reliable way to run IdleTimeWatcher in the user's session at login.

```powershell
# Install — adds HKCU\Software\Microsoft\Windows\CurrentVersion\Run\IdleTimeWatcher
IdleTimeWatcher.exe --install

# Check registration status
IdleTimeWatcher.exe --status

# Remove auto-start
IdleTimeWatcher.exe --uninstall
```

The registry value points to the executable path. On next login, the OS launches the process automatically as the logged-on user, giving it full access to `GetLastInputInfo`.

**Advantages over Scheduled Task:**

- Single command to install/uninstall — no Task Scheduler wizard
- Runs immediately at logon with no polling delay
- Visible and manageable in **Task Manager → Startup apps**

### Option B — Windows Scheduled Task

If group policy or enterprise tooling requires a Scheduled Task, configure it with these settings:

| Setting | Value |
| --- | --- |
| **Trigger** | At logon |
| **Repeat every** | 5 minutes, indefinitely |
| **Run as** | Logged-on user (NOT SYSTEM) |
| **Condition** | Run only if network available |
| **If the task fails, restart every** | 1 minute |
| **Stop the task if it runs longer than** | Disabled |
| **If already running** | Do not start a new instance |

> **Critical:** the task must run as the logged-on user, never as `SYSTEM`. `GetLastInputInfo` returns 0 when called outside the interactive session.

---

## Prometheus Remote Write Integration

Enable in `appsettings.json`:

```json
"PrometheusRemoteWrite": {
  "Enabled": true,
  "Endpoint": "http://your-prometheus-host:9090/api/v1/write"
}
```

The exporter pushes one time series per cycle:

```text
idle_time_seconds{job="idle_time_watcher", instance="HOSTNAME", ...}  <seconds>  <epoch_ms>
```

Labels are sorted lexicographically as required by the Prometheus remote write specification.

### Compatible Backends

**Prometheus** — enable the remote write receiver:

```yaml
# prometheus.yml — or pass --web.enable-remote-write-receiver on the CLI
remote_write_receiver: true
```

**VictoriaMetrics** — the `/api/v1/write` endpoint is enabled by default:

```json
"Endpoint": "http://victoria-metrics:8428/api/v1/write"
```

**Grafana Cloud** — use a bearer token for authentication:

```json
"PrometheusRemoteWrite": {
  "Enabled": true,
  "Endpoint": "https://prometheus-prod-xx.grafana.net/api/prom/push",
  "BearerToken": "glc_eyJ..."
}
```

### Grafana Dashboard

Create a **Time series** panel with this PromQL query:

```promql
idle_time_seconds{instance="HOSTNAME"}
```

Suggested visualization settings:

| Setting | Value |
| --- | --- |
| Unit | `Time → seconds (s)` |
| Min | `0` |
| Threshold (orange) | `300` (5 minutes) |
| Threshold (red) | `1800` (30 minutes) |
| Fill opacity | `10` |

For a per-machine status board, use a **Stat** panel grouped by `instance`:

```promql
idle_time_seconds
```

**Alert — user logged out** (no data for 5 minutes):

```promql
absent_over_time(idle_time_seconds{instance="HOSTNAME"}[5m]) == 1
```

---

## Zabbix Integration

### Item Setup

Create a **Zabbix item** on the target host:

| Field | Value |
| --- | --- |
| Name | `Idle Time` |
| Type | `Zabbix trapper` |
| Key | `idletime` (case-sensitive, must match `Zabbix:ItemKey` in config) |
| Type of information | `Numeric (float)` |
| Units | `s` |

The `zabbix_sender` CLI invocation made by the exporter:

```text
zabbix_sender.exe -z <ServerAddress> -p 10051 -s "<MachineName>" -k idletime -o <seconds>
```

`MachineName` is `Environment.MachineName` — it must match the **Host name** registered in Zabbix exactly (case-sensitive).

### Alerting Triggers

**User logged out** — no data received for 5 minutes:

```text
{HOST.NAME:idletime.nodata(5m)}=1
```

Set up a **Zabbix Action** on this trigger:

- Problem message: `{HOST.NAME} has logged out.`
- Recovery message: `{HOST.NAME} has logged in.`

**Extended absence** — idle for more than 30 minutes while session is still active:

```text
{HOST.NAME:idletime.last()}>1800
```

---

## Development Guide

### Adding a New Exporter

1. Create a class in `Exporters/` that implements `IMetricExporter`:

   ```csharp
   internal sealed class DatadogExporter : IMetricExporter
   {
       public async Task ExportAsync(TimeSpan idleTime, CancellationToken cancellationToken = default)
       {
           // push to Datadog StatsD / HTTP API
       }
   }
   ```

2. Add a corresponding options class to `Models/AppOptions.cs`.

3. Register it in `Program.cs`:

   ```csharp
   services.Configure<DatadogOptions>(ctx.Configuration.GetSection("Datadog"));
   services.AddSingleton<IMetricExporter, DatadogExporter>();
   ```

4. Add a `"Datadog"` section to `appsettings.json`.

`Worker` automatically calls `ExportAsync` on every registered `IMetricExporter` — no other changes needed.

### Running in Debug Mode

Set `"HideConsoleWindow": false` in `appsettings.json` to keep the console window visible. Set `"Default": "Debug"` under `Serilog:MinimumLevel` to see per-cycle idle values.

```bash
cd IdleTime
dotnet run
```

---

## Why Not a Windows Service?

`GetLastInputInfo` only returns valid data when called from a process running in the **interactive user session** (Session 1+). Windows Services run in **Session 0**, which is isolated from the desktop and has no keyboard/mouse input — calling `GetLastInputInfo` from a service always returns 0.

Comparison of user-space auto-start approaches:

| Approach | Runs in User Session | Easy to Deploy | Restart on Failure |
| --- | --- | --- | --- |
| Registry Run key (`--install`) | Yes | One command | Via Task Manager Startup |
| Startup folder shortcut | Yes | Copy file | Manual |
| Scheduled Task (logon trigger) | Yes | Task Scheduler wizard | Built-in restart settings |
| Windows Service (SYSTEM) | No | `sc create` | Built-in |

The **registry Run key** is the lowest-friction option with no external tooling dependency.

---

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).
