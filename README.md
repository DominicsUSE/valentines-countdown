# Roblox Optimizer

A tiny, single-window Windows app that makes Roblox run a bit smoother with
one click. Everything it changes lives in your own Windows user account -
it never asks for administrator permission, never touches antivirus/
firewall/Windows Update, and every change has an Undo.

## What it does

- **Dashboard**: shows whether Roblox is running, current CPU and RAM usage,
  and ping to a public Internet server (a general connection check, not
  your exact Roblox server ping - it's labeled that way in the app).
- **Optimize for Roblox** (one button):
  - Switches Windows to the "High performance" power plan.
  - Turns off the Xbox Game Bar overlay (Settings > Gaming > Xbox Game Bar -
    the same switch, just flipped from the app).
  - If Roblox is open: sets it to use your dedicated graphics card (on
    laptops/PCs with two GPUs) and slightly raises its process priority
    (never to Realtime).
- **Undo Changes** (one button): puts all of the above back exactly how it
  was.

All of this is stored under `HKEY_CURRENT_USER`, so none of it requires an
admin prompt. If you close the app (or it crashes) while changes are still
applied, it also keeps a small backup file at
`%LOCALAPPDATA%\RobloxOptimizer\backup.txt` so the next time you open it,
Undo still works.

## What it will never do

- Ask for administrator rights.
- Touch antivirus software, the Windows Firewall, or Windows Update.
- Modify Roblox's own files, settings, or FastFlags, or read its memory.
- Promise a specific FPS number or "zero ping" - performance still depends
  on your hardware, your internet connection, and the specific Roblox
  experience/server you're in.

## Accessibility

- Every control has a screen-reader-friendly name and description
  (`AutomationProperties.Name`/`HelpText`), works with Windows' Narrator/JAWS.
- Full keyboard support: Tab between controls, **Alt+O** for Optimize,
  **Alt+U** for Undo, **Enter** anywhere triggers Optimize (the default
  action), and every button shows a visible focus outline.
- The activity log is a "live region" (`AutomationProperties.LiveSetting`),
  so a screen reader announces new status messages (like "Done - click Undo
  Changes...") as they appear, without you needing to move focus to read them.
- Text and borders use Windows' own system colors/font size instead of
  hardcoded values, so the app respects your chosen theme, Windows High
  Contrast mode, and "Make text bigger" accessibility settings.
- The window is resizable (not locked to one size) for anyone who needs a
  larger window or bigger text.

## Get a ready-to-run copy without installing anything

Every push to this branch automatically builds a Windows `.exe` via GitHub
Actions - no need to install the .NET SDK just to try it:

1. Open this repository on GitHub and click the **Actions** tab.
2. Open the latest **"Build Roblox Optimizer (Windows)"** run (or click
   **Run workflow** to start one on demand).
3. Once it finishes (~1-2 minutes), download the **RobloxOptimizer-win-x64**
   artifact - it's a zip containing one self-contained `RobloxOptimizer.exe`
   you can run directly, no install required.

Windows SmartScreen may warn about an unsigned executable from an unknown
publisher the first time you run it (`More info` > `Run anyway`) - that's
expected for an app that isn't code-signed yet, not a sign anything is wrong.

## Requirements (only if you want to build it yourself instead)

- Windows 10 or Windows 11.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows Desktop workload).

## Build and run

```powershell
git clone <this-repository>
cd valentines-countdown
dotnet run --project src\RobloxOptimizer\RobloxOptimizer.csproj
```

Or open `RobloxOptimizer.sln` in Visual Studio 2022 and press F5.

To build a single-file executable you can copy to another PC:

```powershell
dotnet publish src\RobloxOptimizer\RobloxOptimizer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## How it's built

One WPF project (`src/RobloxOptimizer/`), two code files:

- `MainWindow.xaml` - the whole UI.
- `MainWindow.xaml.cs` - the whole app: reads CPU/RAM/ping/Roblox status
  every 2 seconds, and implements Optimize/Undo directly against the
  Windows registry and `powercfg`, with a small on-disk backup file for
  crash resilience. No dependency-injection container, no separate
  projects for each concern - deliberately kept to one file per concern so
  it's easy to read top to bottom.
