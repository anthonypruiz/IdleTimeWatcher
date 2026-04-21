# agents.md — AI Agent Guide for IdleTimeWatcher

This file tells AI coding agents (Claude Code, GitHub Copilot Workspace, Cursor, etc.) how to work safely and effectively in this repository.

---

## What This Repository Does

IdleTimeWatcher is a **.NET 8.0 Windows** console application. It P/Invokes `GetLastInputInfo` from `user32.dll` to read keyboard/mouse idle time, then exports that value to **Prometheus Remote Write** and/or **Zabbix** on a randomized 2–10 second interval. It must run inside the logged-on user's interactive Windows session — not as a Windows Service.

---

## Repository Layout

```text
IdleTimeWatcher/
├── IdleTime/                     .NET project root
│   ├── IdleTime.csproj           SDK-style, net8.0-windows, no code-gen steps
│   ├── appsettings.json          Runtime config — copied to output on build
│   ├── Program.cs                Generic Host wiring + --install/--uninstall/--status CLI
│   ├── Worker.cs                 BackgroundService loop
│   ├── Models/AppOptions.cs      IOptionsMonitor<T> config classes (hot-reload capable)
│   ├── Windows/
│   │   ├── IdleTimeDetector.cs   P/Invoke: GetLastInputInfo → TimeSpan
│   │   ├── ConsoleHider.cs       P/Invoke: GetConsoleWindow / ShowWindow
│   │   └── StartupManager.cs     HKCU registry Run key management
│   └── Exporters/
│       ├── IMetricExporter.cs    Interface: ExportAsync(TimeSpan, CancellationToken)
│       ├── ZabbixExporter.cs     Shells out to zabbix_sender.exe
│       ├── ProtoEncoder.cs       Hand-rolled Prometheus remote_write 1.0 protobuf encoder
│       └── PrometheusRemoteWriteExporter.cs  HTTP client for remote write
├── agents.md                     This file
├── CLAUDE.md                     Claude Code specific guidance (read this too)
├── README.md                     End-user documentation
└── LICENSE                       GNU GPLv3
```

---

## Environment and Tooling

| Tool | Version | Notes |
| --- | --- | --- |
| .NET SDK | 8.0 | Required to build |
| OS | Windows 10/11 | Target platform; some Windows APIs are unavailable on Linux/macOS |
| `zabbix_sender.exe` | any | Only needed at runtime if `Zabbix:Enabled=true` |

Verify SDK availability before attempting to build:

```bash
dotnet --version   # must be 8.x
```

---

## Build Commands

All commands run from `IdleTime/` unless noted.

```bash
# Restore NuGet packages and build (debug)
dotnet build

# Build release
dotnet build -c Release

# Publish self-contained single-file executable for deployment
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Run locally (keep console visible for output)
# First set "HideConsoleWindow": false in appsettings.json
dotnet run
```

A clean build with **zero errors and zero warnings** is the baseline. Always verify with `dotnet build` after any code change before considering the task done.

---

## Permitted Autonomous Operations

Agents may perform the following without asking for confirmation:

- Read any source file, config file, or documentation
- Edit source files to fix bugs, add features, or refactor
- Create new files within the existing directory structure (e.g., new exporters in `Exporters/`, new options in `Models/`)
- Update `appsettings.json` schema documentation in README/CLAUDE
- Run `dotnet build` and `dotnet restore`
- Update `agents.md`, `CLAUDE.md`, and `README.md`
- Add or update NuGet package references in `IdleTime.csproj`

---

## Operations That Require Human Confirmation

Do NOT perform the following without explicit user approval:

- Running `IdleTimeWatcher.exe --install` or `--uninstall` — modifies the user's registry
- Running `dotnet publish` with deployment to a production path
- Pushing commits or opening pull requests
- Modifying `appsettings.json` values that contain real IP addresses, tokens, or paths (the defaults in the repo are placeholders — treat them as placeholders)
- Deleting any source files

---

## Prohibited Operations

These must never be performed under any circumstances:

- Do not run the compiled `IdleTimeWatcher.exe` binary in CI or automated contexts — it will attempt to read user input state and modify the registry
- Do not commit secrets, real IP addresses, or real bearer tokens into `appsettings.json`
- Do not bypass the `CancellationToken` threading model (no `Thread.Abort`, no blocking `.Result` on async calls inside the host)
- Do not target a framework other than `net8.0-windows` — the P/Invoke calls require the Windows headers

---

## Key Invariants

These must remain true at all times:

1. **Labels must be sorted lexicographically by name** before being passed to `ProtoEncoder.EncodeWriteRequest`. `PrometheusRemoteWriteExporter.BuildLabels()` enforces this with `labels.Sort(...)`. Never remove that sort.
2. **`Environment.MachineName`** is the Zabbix host identifier and the Prometheus `instance` label. It must not be replaced with a configurable override without also updating both integration docs.
3. **`IMetricExporter` is the only extension point** for adding new metric destinations. `Worker` must not directly reference any exporter class — it depends on `IEnumerable<IMetricExporter>`.
4. **`GetLastInputInfo` requires an interactive session.** Any code that calls `IdleTimeDetector.GetIdleTime()` outside the user's desktop session will receive a garbage result or an `InvalidOperationException`.
5. **`appsettings.json` is always copied to the output directory.** The `<CopyToOutputDirectory>Always</CopyToOutputDirectory>` item in `IdleTime.csproj` must not be removed.

---

## Common Tasks

### Adding a New Metric Sink

1. Create `Exporters/YourExporter.cs` implementing `IMetricExporter`:

   ```csharp
   internal sealed class YourExporter : IMetricExporter
   {
       public async Task ExportAsync(TimeSpan idleTime, CancellationToken cancellationToken = default)
       {
           // your implementation
       }
   }
   ```

2. Add an options class to `Models/AppOptions.cs`.

3. In `Program.cs`, inside `ConfigureServices`:

   ```csharp
   services.Configure<YourOptions>(ctx.Configuration.GetSection("YourSection"));
   services.AddSingleton<IMetricExporter, YourExporter>();
   ```

4. Add the corresponding section to `appsettings.json`.

5. Run `dotnet build` — zero errors required.

6. Update `README.md` (Tech Stack, Features) and `CLAUDE.md` (Architecture, NuGet Packages if applicable).

### Modifying the Protobuf Encoder

`ProtoEncoder.cs` is a hand-rolled implementation of the Prometheus remote_write 1.0 protobuf wire format. The relevant schema fields are:

```text
WriteRequest (field 1: repeated TimeSeries, field 3: repeated MetricMetadata)
  TimeSeries  (field 1: repeated Label,    field 2: repeated Sample)
    Label     (field 1: string name,        field 2: string value)
    Sample    (field 1: double value,        field 2: int64 timestamp_ms)
  MetricMetadata (field 1: MetricType enum, field 2: string metric_family_name,
                  field 4: string help,      field 5: string unit)
```

Wire types used: `0` = varint, `1` = 64-bit (double), `2` = length-delimited (string/message).
Tag encoding: `(field_number << 3) | wire_type`.

When adding new fields, preserve the field numbers exactly — protobuf field numbers are part of the wire format contract and changing them breaks deserialization on the server side.

### Changing Configuration Schema

Any change to `AppOptions.cs` requires matching changes in:

- `appsettings.json` (add the new key with a safe default)
- The configuration table in `CLAUDE.md`
- The Configuration section in `README.md`

### Updating Auto-Start Behavior

`StartupManager.cs` writes to `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`. Any change here must be tested manually — it cannot be verified in a build-only CI pipeline.

---

## Code Style Conventions

This project follows the defaults applied by the .NET 8 SDK:

- **File-scoped namespaces** (`namespace Foo;` not `namespace Foo { }`)
- **Top-level statements** in `Program.cs`
- **`sealed`** on all concrete classes unless inheritance is required
- **`internal`** visibility for everything not part of a public API (there is no public API)
- **No comments** unless the reason is non-obvious — prefer self-documenting names
- **`IOptionsMonitor<T>` pattern** for all configuration — no direct `IConfiguration` access in service classes; always call `_monitor.CurrentValue` inside the method, never cache `.Value` at construction time
- **Async all the way** — every `ExportAsync` implementation must be genuinely async; do not wrap synchronous code in `Task.Run`
- **Nullable reference types enabled** — address all nullable warnings; do not use `!` to suppress unless the nullability is truly impossible

---

## Verification Checklist

After any code change, confirm:

- [ ] `dotnet build` exits with 0 errors, 0 warnings
- [ ] `appsettings.json` schema is consistent with `AppOptions.cs`
- [ ] If `ProtoEncoder.cs` was changed: label sort is still present in `BuildLabels()`, field numbers are unchanged
- [ ] If a new exporter was added: it is registered in `Program.cs` and its `Enabled` flag is checked first inside `ExportAsync`
- [ ] If public-facing behavior changed: README.md and CLAUDE.md are updated
