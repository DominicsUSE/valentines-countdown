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

## Requirements

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
