# Roblox Optimizer

A tiny, single-window Windows app that makes Roblox run a bit smoother with
one click. Everything it changes lives in your own Windows user account -
it never asks for administrator permission, never touches antivirus/
firewall/Windows Update, and every change has an Undo.

## What it does

**Optimize tab**

- **Live status**: whether Roblox is running, current CPU and RAM usage,
  and ping to a public Internet server - current latency plus jitter and
  packet loss from 3 quick samples each refresh (a general connection
  check, not your exact Roblox server ping - it's labeled that way in the
  app; nothing can discover that). Every couple of minutes the app quickly
  checks a few well-known low-latency resolvers (1.1.1.1 / 8.8.8.8 / 9.9.9.9)
  and keeps reading from whichever is currently fastest for your connection,
  so the number shown is always the best real reading available - not a
  faked-lower one. No app can reduce your actual network latency to
  Roblox's own game servers; that depends on your ISP, Wi-Fi vs. Ethernet,
  and the specific server/region the game puts you on.
- **Optimize for Roblox** (one button):
  - Switches Windows to the "High performance" power plan.
  - Turns off the Xbox Game Bar overlay (Settings > Gaming > Xbox Game Bar -
    the same switch, just flipped from the app).
  - If Roblox is open: sets it to use your dedicated graphics card (on
    laptops/PCs with two GPUs) and raises its process priority to **High**
    (one step below Realtime, which is deliberately never used since it can
    make the rest of the PC feel frozen).
- **Undo Changes** (one button): puts all of the above back exactly how it
  was.
- **Boost FPS** card:
  - **Close Background Apps**: gracefully asks a curated list of common
    consumer apps (Discord, Spotify, Steam, Chrome/Edge/Firefox, Slack,
    Teams) to close, if they're running, freeing up CPU/RAM for Roblox.
    This is a polite "please close" request (`CloseMainWindow`), never a
    forced kill, so any of them can still show their own save prompt - and
    it deliberately never scans for or touches anything outside that list
    (a generic "kill whatever's using RAM" scan could just as easily catch
    antivirus, VPN, or backup software).
  - A tip pointing at **Roblox's own** Graphics Quality slider (gear icon
    &rarr; Settings) for lowering mesh/shadow/texture detail, and Roblox's
    own Shift+F5 overlay for a live in-game FPS/ping counter - this app
    never edits Roblox's files or FastFlags itself (see "What it will
    never do" below).

All of this is stored under `HKEY_CURRENT_USER`, so none of it requires an
admin prompt. If you close the app (or it crashes) while changes are still
applied, it also keeps a small backup file at
`%LOCALAPPDATA%\RobloxOptimizer\backup.txt` so the next time you open it,
Undo still works.

**Voice Effects tab**

- **Record & play back**: record 5 seconds from your mic, then play it back
  through your speakers with a fun effect (Normal, Deep, High, Robot).
- **Live Voice Changer**: continuously applies the selected effect to your
  mic in real time and sends it to whichever output device you pick from
  the dropdown - your normal speakers by default, or a virtual audio cable
  if you've installed one (see the tip in the app / below). "Deep"/"High"
  in live mode use a lightweight per-chunk resampling trick to keep timing
  in sync, so they can sound a bit more textured than the pre-recorded
  version above; "Robot" sounds the same either way.
- **Louder output**: an adjustable **Volume boost** slider (1x-3x, default
  2x) applies to every effect (recorded playback and live), so the
  processed voice comes through louder than your raw mic input. Very loud
  peaks are clamped rather than distorted/wrapped - the normal, safe way
  software raises loudness.
- **Mic input level meter**: a live bar that moves while you talk, during
  either recording or the live voice changer - the fastest way to tell
  whether the app is actually receiving your microphone at all, before
  troubleshooting anything else.
- **Test output device**: plays a short tone on whichever output device
  is selected, independent of your microphone - confirms a virtual audio
  cable is wired up correctly without needing to talk or open Roblox.
- **Check My Setup**: a self-serve diagnostic for "I set it up but the
  other app still doesn't hear it" - checks whether a virtual cable is
  installed and whether it's the one selected as the output device above
  (the two things this app can actually see), and is explicit that the
  third thing - whether Roblox/Telegram/etc.'s own microphone setting
  points at that same cable - lives entirely inside that other app, so
  only you can check/change it there.
- To have a live-changed voice actually heard inside Roblox, Discord,
  Telegram, Zoom, Teams, Skype, or any other app's voice chat/calls **on
  this PC**: install a free virtual audio cable app yourself (e.g.
  VB-CABLE - not bundled with this project), pick it as the Live Voice
  Changer's output device, then select that same cable as your microphone
  in that other app's own settings. The **"How do I use this in other
  apps/calls?"** button on the Voice Effects tab walks through these
  exact steps -
  without them, picking your normal speakers only lets you hear the
  effect yourself, since Windows doesn't let one app silently replace
  another app's microphone. This can't reach an actual call on your
  phone's cellular network - that's separate hardware this PC has no
  access to.

## What it will never do

- Ask for administrator rights.
- Touch antivirus software, the Windows Firewall, or Windows Update.
- Modify Roblox's own files, settings, or FastFlags, or read its memory.
- Give you cheats, exploits, aimbots, ESP/wallhacks, or any other unfair
  advantage - that violates Roblox's Terms of Service, requires reading the
  game's process memory, and risks your account.
- Overclock your CPU/GPU (adjust clock speeds or voltages) - real
  overclocking can cause instability or hardware damage if done wrong,
  which is a different risk category from the safe, reversible settings
  (power plan, process priority, GPU preference) this app actually changes.
- Clone or impersonate any real person's voice (political figures included).
  The Voice Effects tab only does generic pitch/robot effects - never a
  specific real person.
- Feed a changed voice into Roblox's, Discord's, Zoom's, or any other
  app's voice chat/calls by itself - Windows doesn't allow one app to
  replace another app's microphone input without a virtual audio cable
  driver, and this app doesn't install one for you. Pair it yourself with
  a well-known free tool like VB-Audio Virtual Cable if you want that. It
  also can't reach an actual call on your phone's cellular network -
  that's separate hardware entirely, not something any PC app can touch.
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
Actions and publishes it as a public GitHub Release asset - no GitHub
sign-in and no .NET SDK install required to try it:

**<https://github.com/DominicsUSE/valentines-countdown/releases/download/latest-build/RobloxOptimizer.exe>**

That link always points at the newest build (the `latest-build` release is
replaced on every push) - just download `RobloxOptimizer.exe` and run it
directly, no install or zip extraction required.

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

One WPF project (`src/RobloxOptimizer/`), three code files:

- `MainWindow.xaml` - the whole UI (a two-tab window: Optimize, Voice Effects).
- `MainWindow.xaml.cs` - the Optimize tab: reads CPU/RAM/ping/Roblox status
  every 2 seconds, and implements Optimize/Undo directly against the
  Windows registry and `powercfg`, with a small on-disk backup file for
  crash resilience.
- `MainWindow.VoiceEffects.cs` - the Voice Effects tab: records/plays back
  audio via [NAudio](https://github.com/naudio/NAudio), with the effects
  implemented as plain, simple DSP (playback-rate tricks for Deep/High,
  ring modulation for Robot) - no cloning, no machine learning model.

No dependency-injection container, no separate projects per concern -
deliberately kept to a handful of files so it's easy to read top to bottom.
`.github/workflows/build-roblox-optimizer.yml` builds and verifies it on
real Windows on every push (see "Get a ready-to-run copy" above).
