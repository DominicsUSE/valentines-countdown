# Architecture

Roblox Performance Optimizer ("RPO") is a Windows desktop app (.NET 8, WPF)
that helps a player understand *why* Roblox feels slow and apply safe,
reversible Windows/network settings changes. It never modifies Roblox
itself, never reads game memory, and never claims results it cannot
measure.

## Design principles

1. **Measure, don't guess.** Every number on the dashboard either comes from
   a real OS/API measurement or is shown as "Unavailable" with a reason.
   Nothing is estimated or faked to look complete.
2. **Least privilege.** The main process always runs as a standard user.
   The handful of actions that genuinely need administrator rights (see
   `SAFETY_MODEL.md`) are executed by briefly relaunching the app with a
   single elevated, whitelisted command — never a fully elevated UI.
3. **Everything reversible.** Every optimization action records the
   previous state before changing anything, exposes an `Undo`, and is also
   captured in a JSON backup snapshot plus an append-only audit log. There
   is a single "Restore Defaults" entry point that reverts everything RPO
   has changed.
4. **No game interference.** RPO never edits Roblox's installation files,
   FastFlags, `AppSettings.xml`, or process memory, and never automates
   input into the game. All Roblox-side guidance (e.g. "lower graphics
   quality") is presented as instructions for the user to apply themselves
   inside Roblox's own settings menu.
5. **No dark patterns.** No ads, telemetry, bundled software, or
   auto-updaters that phone home. Settings are stored locally only.

## Module map

| Project | Responsibility |
|---|---|
| `RobloxOptimizer.Core` | Shared models, enums, and interfaces. No Windows-specific code — keeps the domain contracts testable on any OS. |
| `RobloxOptimizer.Monitoring` | CPU/RAM/disk/GPU usage, CPU/GPU temperature (when exposed by firmware), thermal-throttle detection, best-effort per-process FPS via ETW/DXGI tracing. |
| `RobloxOptimizer.Network` | Ping/jitter/packet-loss tests, Wi-Fi vs Ethernet detection, Wi-Fi signal strength, throughput sampling, DNS-flush helper. |
| `RobloxOptimizer.RobloxIntegration` | Detects `RobloxPlayerBeta.exe`, raises launch/exit events, defines the Maximum FPS / Balanced / Visual Quality guidance profiles. |
| `RobloxOptimizer.Optimization` | The reversible optimization actions (power plan, overlays, GPU preference, process priority, startup apps, background apps), the recommendation engine, and the orchestrator that runs "Optimize for Roblox" and auto-restores when Roblox exits. |
| `RobloxOptimizer.Backup` | JSON settings-snapshot backup/restore, best-effort System Restore point creation, and the lazy UAC-elevation helper. |
| `RobloxOptimizer.Logging` | Local, append-only audit log of every setting RPO has changed. |
| `RobloxOptimizer.App` | WPF UI (MVVM), dark theme, Beginner/Advanced modes. |
| `RobloxOptimizer.Tests` | Unit tests for the pure logic (recommendation engine, backup snapshot round-tripping, parsers) that doesn't require live Windows APIs. |

Dependencies only point inward toward `Core`; the UI project is the only
one that depends on all the others (composition root).

## Data flow

```
 ┌────────────────┐     poll (1s)     ┌───────────────────────┐
 │ RobloxDetector │ ───────────────▶  │ MainViewModel          │
 └────────────────┘                   │  (dashboard state)     │
 ┌────────────────┐     poll (1s)     │                        │
 │ SystemMonitor   │ ───────────────▶ │                        │
 └────────────────┘                   │                        │
 ┌────────────────┐   on demand       │                        │
 │NetworkDiagnostics│─────────────────▶                        │
 └────────────────┘                   └──────────┬─────────────┘
                                                  │ snapshot
                                                  ▼
                                     ┌───────────────────────┐
                                     │ RecommendationEngine   │
                                     └──────────┬─────────────┘
                                                  │ recommendations
                                                  ▼
                              ┌─────────────────────────────────┐
                              │ OptimizationOrchestrator         │
                              │  - Apply / Undo per action       │
                              │  - BackupService (before/after)  │
                              │  - AuditLogger (every change)    │
                              └─────────────────────────────────┘
```

## FPS measurement — what it can and can't do

Roblox does not expose an API for its internal frame rate. RPO's FPS
monitor uses the same technique as tools like PresentMon/RTSS: it listens
to the OS's own DXGI/ETW "frame presented" trace events for the Roblox
process ID. This requires the app to be running elevated (ETW real-time
sessions are an OS restriction, not something RPO chooses), and even then
it is a best-effort estimate of render cadence, not an official metric.

If the trace session cannot be started (no admin rights, ETW session
limits reached, or unsupported provider), the dashboard shows **FPS:
Unavailable** with the reason, and points the user to Roblox's own,
authoritative Performance Stats overlay (`Shift+F5` in-game) instead of
guessing a number.

## Regional latency comparison — what it can and can't do

RPO cannot discover which physical server a Roblox game session is
matched to, and it does not attempt to connect to arbitrary game-server
IPs. "Regional endpoint" pings target well-known, publicly reachable
Internet points of presence (e.g. major public DNS resolvers) purely to
gauge general route quality from the user's ISP. The UI explicitly labels
this as an approximation of general Internet path quality, **not** a
measurement of Roblox server latency.

## Reversibility scope

Not every reversible change is reverted the same way:

- **Session-scoped ("Optimize for Roblox" bundle)** - power plan, overlay
  toggle, GPU preference, and process priority. These are captured to the
  backup snapshot on Apply, exposed through Undo/Restore Defaults, and are
  also auto-reverted by `OptimizationOrchestrator` the instant it detects
  Roblox has exited, so nothing is left modified after a play session.
- **Persistent, per-item choices** - startup app enable/disable and
  background-app closures. These are deliberate, standalone decisions the
  user makes one item at a time (e.g. "stop this app from launching at
  sign-in for good"). They are **not** swept up by Roblox exiting or by the
  session bundle's Restore Defaults, because auto-reverting them would
  silently undo a choice the user explicitly made. Each has its own
  dedicated reversal: flipping a startup entry back to Enabled, or simply
  reopening a closed application.

## Elevation model

See `SAFETY_MODEL.md` for the full list of actions and whether each needs
administrator rights. The pattern used when elevation truly is required:

1. The standard-user process shows a clear explanation and an admin-shield
   button.
2. On confirmation, RPO relaunches itself with `runas` and a single
   narrow argument such as `--elevated-action=CreateRestorePoint`.
3. The elevated instance performs *only* that one action, writes a JSON
   result file, and exits immediately — it never leaves an elevated
   window open.
4. The original standard-user process reads the result and continues.
