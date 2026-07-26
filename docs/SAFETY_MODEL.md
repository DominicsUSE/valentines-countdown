# Safety model

This document is the checklist RPO's design is held to. Any new
optimization action must fill in this table before it is added.

## What RPO will never do

- Modify Roblox's installed files, `AppSettings.xml`, FastFlags
  (documented or undocumented), save data, or process memory.
- Automate mouse/keyboard input into Roblox or any other game.
- Disable Windows Update, Windows Defender/antivirus, the Windows
  Firewall, User Account Control, or any service tagged
  `critical`/`security` in `RobloxOptimizer.Optimization`'s protected list.
- Set any process priority to `Realtime`.
- Change router firmware, open ports, or alter TCP/IP stack internals
  (no disabling IPv6, no registry TCP tweaks, no bundled VPN).
- Collect Roblox credentials, passwords, browsing history, keystrokes,
  or file contents. No telemetry is sent anywhere; nothing leaves the
  device.
- Show a fabricated or estimated number where a real measurement isn't
  available — it is labeled **Unavailable** instead.
- Install bundled third-party software, browser extensions, ads, or
  cryptocurrency miners.

## Administrator rights — action by action

| Action | Needs admin? | Why |
|---|---|---|
| Switch Windows power plan (Balanced/High performance) | No | Selecting an *existing* power scheme is a per-user operation. |
| Toggle Xbox Game Bar / Game DVR overlay | No | Stored under `HKCU`. |
| Set Roblox's GPU preference (dedicated GPU) | No | Stored under `HKCU\...\DirectX\UserGpuPreferences`. |
| Raise Roblox's process priority to Above Normal | No | A process can lower/raise its own-user-owned process priority up to `High` without elevation; RPO caps at `AboveNormal`. |
| Disable a per-user startup entry (`HKCU\...\Run`, user Startup folder) | No | Owned by the current user. |
| Disable an all-users startup entry (`HKLM\...\Run`, common Startup folder, Scheduled Tasks) | **Yes** | Writing under `HKLM` or the all-users Startup folder requires admin. |
| Close a selected background application | No | Standard `Process` termination of a same-user process. |
| Flush DNS cache | No | `ipconfig /flushdns` works for standard users on current Windows releases. |
| Create a System Restore point | **Yes** | System Restore is an admin-only API. |
| Read CPU/GPU/RAM/disk usage, ping, Wi-Fi signal | No | Read-only. |
| ETW-based FPS tracing | **Yes** | Real-time ETW trace sessions require an elevated token (or membership in "Performance Log Users"). |

RPO requests elevation **only** at the moment one of the two "Yes" rows
above is actually invoked, using the narrow relaunch pattern described in
`ARCHITECTURE.md`. The main window and dashboard never require
elevation to view.

## Reversibility guarantee

Every entry in `RobloxOptimizer.Optimization.Actions` implements:

```csharp
Task<ActionResult> ApplyAsync(CancellationToken ct);
Task<ActionResult> UndoAsync(CancellationToken ct);
```

Before `ApplyAsync` runs, the orchestrator asks the action for its
current state via `CaptureStateAsync()` and writes it to the active
`BackupSnapshot`. `Undo Changes` and `Restore Defaults` both replay the
snapshot. Process-priority changes are additionally auto-reverted the
moment RPO detects Roblox has exited, so nothing is left in a modified
state after a play session — even if the user forgets to click Undo.

## Confirmation and transparency

- Every recommendation card shows: the detected problem, the proposed
  change, expected benefit, possible side effects, whether admin rights
  are required, and Apply/Undo buttons — before anything is changed.
- "Apply Recommended Fixes" shows the full list with checkboxes (all
  unchecked items are skipped) and requires one confirmation click.
- No setting is changed silently or on a timer without the user having
  clicked Apply first, except restoring a temporary change back to its
  original value, which is documented up front as automatic.

## Data handling

- All settings, backups, and the audit log are stored under
  `%LOCALAPPDATA%\RobloxOptimizer\` as plain JSON/text files.
- No network calls are made except the diagnostic ICMP pings the user
  explicitly triggers from the Network tab.
- No analytics SDK, crash reporter, or update-checker is included.

## Honesty in claims

- The app never promises "zero ping," a guaranteed FPS number, or that
  it can fix hardware/ISP limitations it cannot change.
- Every recommendation is phrased as "may help" / "expected benefit"
  language tied to the specific measurement that triggered it, and the
  before/after benchmark is the only place a numeric improvement is
  claimed — and only after it was actually re-measured.
