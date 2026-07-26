# Roblox Performance Optimizer (RPO)

A Windows 10/11 desktop app (.NET 8, WPF) that helps you understand *why*
Roblox feels laggy and apply safe, transparent, fully-reversible Windows,
network, and Roblox-adjacent settings changes. It cannot exceed your
hardware's limits or reduce Internet latency below what your connection and
physical distance to a server allow, and it says so.

See `docs/ARCHITECTURE.md` for the design/safety model and `docs/SAFETY_MODEL.md`
for the complete "what this will and will never do" checklist before reading
any code.

## Directory structure

```
RobloxPerformanceOptimizer/
├── RobloxPerformanceOptimizer.sln
├── Directory.Build.props            # shared MSBuild settings (Nullable, ImplicitUsings, ...)
├── docs/
│   ├── ARCHITECTURE.md              # module map, data flow, elevation model
│   └── SAFETY_MODEL.md              # admin-rights table, reversibility guarantee
├── src/
│   ├── RobloxOptimizer.Core/            # models, enums, interfaces - no Windows API calls
│   ├── RobloxOptimizer.Monitoring/       # CPU/RAM/GPU/disk, temperature, throttle, FPS tracing
│   ├── RobloxOptimizer.Network/          # ping/jitter/packet loss, Wi-Fi signal, DNS flush
│   ├── RobloxOptimizer.RobloxIntegration/# RobloxPlayerBeta.exe detection, guidance profiles
│   ├── RobloxOptimizer.Optimization/     # reversible actions + recommendation engine
│   ├── RobloxOptimizer.Backup/           # JSON settings backup, System Restore point
│   ├── RobloxOptimizer.Logging/          # local audit log
│   └── RobloxOptimizer.App/              # WPF UI (composition root)
│       ├── app.manifest                 # requestedExecutionLevel="asInvoker"
│       ├── App.xaml(.cs)                # DI wiring + elevated-helper dispatch
│       ├── Elevation/                   # briefly-elevated helper process pattern
│       ├── ViewModels/, Views/, Converters/, Themes/, Helpers/
└── tests/
    └── RobloxOptimizer.Tests/            # xUnit tests for pure logic
```

Every project belongs under `src/<ProjectName>/`, matching its assembly
name; there is one `.csproj` per folder. `RobloxOptimizer.App` is the only
project that references all the others (composition root) - see the
dependency diagram in `docs/ARCHITECTURE.md`.

## Requirements

- Windows 10 (1809+) or Windows 11.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows Desktop workload included).
- Visual Studio 2022 17.8+ (optional, for the IDE experience) or just the `dotnet` CLI.

## Build and run

```powershell
git clone <this-repository>
cd RobloxPerformanceOptimizer
dotnet restore
dotnet build -c Release
dotnet run --project src\RobloxOptimizer.App\RobloxOptimizer.App.csproj -c Release
```

To produce a single-folder, self-contained build you can copy to another
Windows PC without installing the .NET runtime separately:

```powershell
dotnet publish src\RobloxOptimizer.App\RobloxOptimizer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The published `RobloxOptimizer.exe` in `publish\` runs standalone. It never
launches elevated by itself (see `app.manifest`); a UAC prompt only appears
the moment you use one of the two features that genuinely need it
(creating a System Restore point, or disabling an all-users startup entry) -
see `docs/SAFETY_MODEL.md`.

## Running the tests

```powershell
dotnet test tests\RobloxOptimizer.Tests\RobloxOptimizer.Tests.csproj
```

The test project targets `net8.0-windows` and covers the pure logic that
doesn't require live hardware/registry access: the recommendation engine's
FPS-vs-network-lag classification and thresholds, the Wi-Fi signal parser
(against captured sample `netsh` output), the settings-backup round trip,
and the `Measurement<T>` "never fabricate a value" contract.

## What each stage of this build covers

1. **Architecture and safety model** - `docs/ARCHITECTURE.md`, `docs/SAFETY_MODEL.md`.
2. **Directory structure** - above, and the `src/`/`tests/` tree itself.
3. **Performance-monitoring dashboard** - `RobloxOptimizer.Monitoring` (`SystemMonitor`, `TemperatureMonitor`, `FpsMonitor`) surfaced on `Views/DashboardView.xaml`.
4. **Roblox process detection** - `RobloxOptimizer.RobloxIntegration` (`RobloxDetector`, `RobloxProfileCatalog`).
5. **Network diagnostics** - `RobloxOptimizer.Network` (`NetworkDiagnostics`, `WifiSignalReader`, `RegionalEndpoints`).
6. **Safe, reversible optimization actions** - `RobloxOptimizer.Optimization` (`Actions/*`, `RecommendationEngine`, `OptimizationOrchestrator`).
7. **Backup and restore** - `RobloxOptimizer.Backup` (`BackupService`, `RestorePointService`), `RobloxOptimizer.Logging` (`AuditLogger`).
8. **Error handling and automated tests** - defensive try/catch throughout every Windows API call (each monitor/diagnostic degrades to "Unavailable" rather than throwing/crashing), plus `tests/RobloxOptimizer.Tests`.
9. **Build and run instructions** - this section.
10. **Security/privacy/admin-risk/misleading-claims review** - see "Self-review" below.

## Self-review notes

- **Administrator rights**: requested only via the narrow, whitelisted
  relaunch in `RobloxOptimizer.App/Elevation`, for exactly the two actions
  listed in `docs/SAFETY_MODEL.md` (System Restore point, all-users startup
  entries). The main window and every other feature run as a standard user.
- **Privacy**: no network calls other than the diagnostic ICMP pings the
  user explicitly triggers; no credentials, browsing history, or file
  contents are ever read; nothing is sent off the device. All state lives
  under `%LOCALAPPDATA%\RobloxOptimizer\`.
- **No misleading claims**: every dashboard value is either a real
  measurement or the literal word "Unavailable" with a reason
  (`Measurement<T>` in `RobloxOptimizer.Core`) - see especially the FPS
  monitor and regional-ping sections of `docs/ARCHITECTURE.md` for the two
  places this app is most tempted to guess, and doesn't.
- **Reversibility**: every `IOptimizationAction` implements capture/apply/undo
  against the same backup snapshot; the "Optimize for Roblox" bundle is
  additionally auto-reverted the instant `RobloxDetector` reports Roblox has
  exited (`OptimizationOrchestrator.OnRobloxExited`).
- **Roblox rules**: nothing here reads Roblox's memory, edits its files or
  FastFlags, or automates input into the game - confirmed by grep across
  `RobloxOptimizer.RobloxIntegration` and `RobloxOptimizer.Optimization`.
