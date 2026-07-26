using RobloxOptimizer.Core;
using RobloxOptimizer.Core.Constants;
using RobloxOptimizer.Core.Enums;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Optimization;

/// <summary>
/// Turns one <see cref="DiagnosticsSnapshot"/> into a prioritized, fully
/// transparent list of recommendations. Trigger points reuse the exact same
/// thresholds as <see cref="MetricStatusEvaluator"/> (the dashboard's
/// Good/Warning/Critical color-coding), so "why is this recommended" always
/// matches what the user sees. If a measurement is unavailable, the
/// recommendation it would have fed is simply skipped rather than guessed at.
/// </summary>
public static class RecommendationEngine
{
    // Bandwidth contention (kilobits/sec) - well above what Roblox itself needs. Not a dashboard status metric, so it isn't in MetricStatusEvaluator.
    private const double HighThroughputKbps = 8000;

    public static DiagnosticsResult Analyze(DiagnosticsSnapshot snapshot)
    {
        var causes = LagCause.None;
        var recommendations = new List<Recommendation>();
        var priority = 0;

        var fps = snapshot.Fps.Average;
        var ping = snapshot.Network.PingAverageMs;
        var jitter = snapshot.Network.JitterMs;
        var packetLoss = snapshot.Network.PacketLossPercent;

        var fpsIsLow = MetricStatusEvaluator.ForFps(fps) is StatusLevel.Warning or StatusLevel.Critical;
        var networkIsBad = MetricStatusEvaluator.ForNetworkOverall(snapshot.Network) is StatusLevel.Warning or StatusLevel.Critical;

        // --- FPS vs network lag explanation (dashboard requirement) ---
        string lagExplanation;
        if (fpsIsLow && networkIsBad)
        {
            lagExplanation =
                "Both FPS and network issues were detected. FPS lag looks like the picture itself is choppy or freezing - " +
                "that's a rendering/CPU/GPU problem. Network lag looks like the picture is smooth but your actions take a moment to register, " +
                "or other players/objects jump/teleport - that's a connection problem. You appear to have some of both, so fixing one may not fully fix the other.";
        }
        else if (fpsIsLow)
        {
            lagExplanation =
                "This looks like FPS lag, not network lag: your average frame rate is low, which shows up as a choppy or freezing picture. " +
                "Network-side numbers (ping/jitter/packet loss) look fine, so your actions should still register promptly even though the visuals stutter.";
        }
        else if (networkIsBad)
        {
            lagExplanation =
                "This looks like network lag, not FPS lag: your frame rate looks fine, so the picture itself should be smooth. " +
                "Elevated ping/jitter/packet loss means your actions may take longer to register, and other players or objects can appear to " +
                "rubber-band or teleport.";
        }
        else
        {
            lagExplanation =
                "No significant FPS or network problems were detected in this pass. If Roblox still feels laggy, try Analyze again while you're " +
                "actually in an experience, since performance varies a lot between different Roblox places and servers.";
        }

        // --- FPS ---
        var fpsStatus = MetricStatusEvaluator.ForFps(fps);
        if (fpsStatus == StatusLevel.Critical)
        {
            causes |= LagCause.LowFps;
            recommendations.Add(new Recommendation
            {
                ActionId = null,
                Cause = LagCause.LowFps,
                Title = "Lower Roblox's graphics quality",
                DetectedProblem = $"Average frame rate measured at {fps.Value:0.#} FPS, which is low.",
                ProposedChange = "In Roblox's Settings menu, lower the Graphics Quality slider (or turn off Graphics Quality: Automatic and pick a low fixed level).",
                ExpectedBenefit = "Lower graphics quality reduces GPU/CPU rendering work per frame, which typically raises and stabilizes FPS.",
                PossibleSideEffects = "Shadows, reflections, and draw distance will look simpler.",
                RequiresAdmin = false,
                Priority = priority++,
                IsAutomatable = false
            });
        }
        else if (fpsStatus == StatusLevel.Warning)
        {
            causes |= LagCause.LowFps;
        }

        // --- Overheating / thermal throttling ---
        if (snapshot.Temperature.CpuThrottleDetected is { IsAvailable: true, Value: true })
        {
            causes |= LagCause.Overheating;
            var tempText = snapshot.Temperature.CpuTemperatureCelsius.IsAvailable
                ? $" (CPU currently reading {snapshot.Temperature.CpuTemperatureCelsius.Value:0.#}°C)"
                : string.Empty;

            recommendations.Add(new Recommendation
            {
                ActionId = null,
                Cause = LagCause.Overheating,
                Title = "Address CPU overheating / thermal throttling",
                DetectedProblem = $"Your CPU appears to be running well below its rated clock speed under load{tempText}, which is consistent with thermal throttling.",
                ProposedChange = "Clean dust from intake/exhaust vents and fans, make sure the PC/laptop has clear airflow, and consider a cooling pad (laptops) or reapplying thermal paste (desktops, advanced users only).",
                ExpectedBenefit = "Reducing temperatures lets the CPU sustain its normal clock speed instead of slowing itself down to avoid damage, which directly improves FPS stability.",
                PossibleSideEffects = "None from cleaning/airflow changes. Reapplying thermal paste requires opening the case and can void warranties - only attempt this if you're comfortable doing so.",
                RequiresAdmin = false,
                Priority = priority++,
                IsAutomatable = false
            });
        }

        // --- Network: high ping ---
        if (MetricStatusEvaluator.ForPing(ping) is StatusLevel.Warning or StatusLevel.Critical)
        {
            var isStableAndElevated = MetricStatusEvaluator.ForJitter(jitter) == StatusLevel.Good &&
                                        MetricStatusEvaluator.ForPacketLoss(packetLoss) == StatusLevel.Good;

            if (isStableAndElevated)
            {
                causes |= LagCause.ServerDistance;
                recommendations.Add(new Recommendation
                {
                    ActionId = null,
                    Cause = LagCause.ServerDistance,
                    Title = "Ping is high but stable - likely server distance",
                    DetectedProblem = $"Average ping is {ping.Value:0} ms and consistent (low jitter, little/no packet loss), which usually means the round trip to the server is simply long, not that your connection is unstable.",
                    ProposedChange = "If the Roblox experience you're playing offers a server list or 'rejoin' option, try switching servers. Otherwise this is likely a physical-distance limit that settings changes can't fix.",
                    ExpectedBenefit = "A geographically closer server can reduce ping substantially; RPO cannot guarantee one is available for every experience.",
                    PossibleSideEffects = "None.",
                    RequiresAdmin = false,
                    Priority = priority++,
                    IsAutomatable = false
                });
            }
            else
            {
                causes |= LagCause.HighPing;
                recommendations.Add(new Recommendation
                {
                    ActionId = null,
                    Cause = LagCause.HighPing,
                    Title = "Reduce network congestion",
                    DetectedProblem = $"Average ping is {ping.Value:0} ms and unstable (jitter and/or packet loss also elevated), suggesting congestion rather than pure distance.",
                    ProposedChange = "Close bandwidth-heavy downloads/streams/cloud sync on your network, and if you're on Wi-Fi, move closer to the router or switch to Ethernet.",
                    ExpectedBenefit = "Freeing up your connection reduces queuing delay, which can lower and stabilize ping.",
                    PossibleSideEffects = "None - this only asks you to pause other network activity, it does not change any settings automatically.",
                    RequiresAdmin = false,
                    Priority = priority++,
                    IsAutomatable = false
                });
            }
        }

        // --- Network: packet loss ---
        if (MetricStatusEvaluator.ForPacketLoss(packetLoss) is StatusLevel.Warning or StatusLevel.Critical)
        {
            causes |= LagCause.PacketLoss;
        }

        // --- Wi-Fi signal ---
        if (snapshot.Network.ConnectionType == ConnectionType.WiFi)
        {
            var wifiStatus = MetricStatusEvaluator.ForWifiSignal(snapshot.Network.WifiSignalPercent);
            if (wifiStatus is StatusLevel.Warning or StatusLevel.Critical)
            {
                causes |= LagCause.WeakWifi;
            }

            recommendations.Add(new Recommendation
            {
                ActionId = null,
                Cause = LagCause.WeakWifi,
                Title = "Connect through Ethernet",
                DetectedProblem = snapshot.Network.WifiSignalPercent.IsAvailable
                    ? $"You're connected over Wi-Fi with {snapshot.Network.WifiSignalPercent.Value}% signal strength."
                    : "You're connected over Wi-Fi.",
                ProposedChange = "Use a wired Ethernet connection instead of Wi-Fi if one is available.",
                ExpectedBenefit = "Ethernet is typically more stable than Wi-Fi and less prone to jitter and packet loss caused by interference or distance from the router.",
                PossibleSideEffects = "Requires running a cable to your PC.",
                RequiresAdmin = false,
                Priority = wifiStatus == StatusLevel.Critical ? priority++ : priority + 10,
                IsAutomatable = false
            });
        }

        // --- Bandwidth contention ---
        var download = snapshot.Network.DownloadKilobitsPerSecond;
        var upload = snapshot.Network.UploadKilobitsPerSecond;
        if ((download.IsAvailable && download.Value > HighThroughputKbps) || (upload.IsAvailable && upload.Value > HighThroughputKbps))
        {
            causes |= LagCause.BandwidthContention;
            recommendations.Add(new Recommendation
            {
                ActionId = null,
                Cause = LagCause.BandwidthContention,
                Title = "Close bandwidth-heavy applications",
                DetectedProblem = "Significant network throughput was measured on your connection during this test (large download, cloud sync, or streaming).",
                ProposedChange = "Pause large downloads, cloud backup/sync (OneDrive, Google Drive, Dropbox, etc.), and video streaming while you play.",
                ExpectedBenefit = "Frees up bandwidth and reduces router/modem queuing delay, which can lower ping and packet loss.",
                PossibleSideEffects = "Paused downloads/sync will resume once you unpause them or close Roblox.",
                RequiresAdmin = false,
                Priority = priority++,
                IsAutomatable = false
            });
        }

        // --- Background applications / CPU pressure ---
        var heavyApps = snapshot.BackgroundApps
            .Where(app => (app.CpuPercent.IsAvailable && app.CpuPercent.Value >= 10) || (app.MemoryMegabytes.IsAvailable && app.MemoryMegabytes.Value >= 500))
            .OrderByDescending(app => app.CpuPercent.IsAvailable ? app.CpuPercent.Value : 0)
            .Take(5)
            .ToList();

        var cpu = snapshot.System.CpuUsagePercent;
        if (heavyApps.Count > 0 || MetricStatusEvaluator.ForUsagePercent(cpu) is StatusLevel.Warning or StatusLevel.Critical)
        {
            causes |= LagCause.BackgroundApplications;
            var namesText = heavyApps.Count > 0
                ? string.Join(", ", heavyApps.Select(a => a.ProcessName))
                : "several background processes";

            recommendations.Add(new Recommendation
            {
                // Guidance-only: closing a *specific* app requires the user to pick which one on the
                // Optimize tab's Background Apps list - there is no single generic action to invoke here.
                ActionId = null,
                Cause = LagCause.BackgroundApplications,
                Title = "Close unnecessary background applications",
                DetectedProblem = $"CPU usage is elevated" + (heavyApps.Count > 0 ? $", and {namesText} are using significant CPU/RAM." : "."),
                ProposedChange = "Review the Background Apps list on the Optimize tab and close the ones you don't need while playing.",
                ExpectedBenefit = "Frees up CPU/RAM/GPU headroom for Roblox, which can raise and stabilize FPS.",
                PossibleSideEffects = "You'll need to reopen any closed app yourself afterward - closing an app is not something RPO can auto-undo.",
                RequiresAdmin = false,
                Priority = priority++,
                IsAutomatable = false
            });
        }

        // --- Overlays ---
        recommendations.Add(new Recommendation
        {
            ActionId = OptimizationActionIds.DisableOverlays,
            Cause = LagCause.BackgroundApplications,
            Title = "Disable unnecessary overlays",
            DetectedProblem = "Overlays (Xbox Game Bar, Discord, GPU vendor overlays) run alongside Roblox and can use CPU/GPU/RAM, especially when they capture or record.",
            ProposedChange = "Disable Xbox Game Bar / Game DVR (RPO can do this for you), and turn off Discord's and your GPU vendor's in-game overlay from their own settings.",
            ExpectedBenefit = "Removes background rendering/capture work that competes with Roblox for the same CPU/GPU resources.",
            PossibleSideEffects = "You won't be able to use Game Bar/Discord/GPU overlay screenshot & recording shortcuts while disabled.",
            RequiresAdmin = false,
            Priority = priority + 5,
            IsAutomatable = true
        });

        // --- Dedicated GPU ---
        if (snapshot.DetectedGpuCount > 1)
        {
            recommendations.Add(new Recommendation
            {
                ActionId = OptimizationActionIds.GpuPreferenceHighPerformance,
                Cause = LagCause.LowFps,
                Title = "Use the dedicated GPU for Roblox",
                DetectedProblem = $"{snapshot.DetectedGpuCount} graphics adapters were detected on this system (e.g. integrated + dedicated). Windows may be running Roblox on the slower one.",
                ProposedChange = "Set Roblox to always use the high-performance GPU.",
                ExpectedBenefit = "Ensures Roblox renders on your faster graphics card instead of power-saving integrated graphics.",
                PossibleSideEffects = "Slightly higher power/battery use on laptops while Roblox is running.",
                RequiresAdmin = false,
                Priority = priority++,
                IsAutomatable = true
            });
        }

        // --- Power plan ---
        recommendations.Add(new Recommendation
        {
            ActionId = OptimizationActionIds.PowerPlanHighPerformance,
            Cause = LagCause.LowFps,
            Title = "Switch to a high-performance power plan",
            DetectedProblem = "Windows' default Balanced power plan intentionally limits CPU boost speed to save power/battery.",
            ProposedChange = "Switch to a High performance power plan while you play, then automatically restore your previous plan afterward.",
            ExpectedBenefit = "Lets the CPU run at its full available clock speed instead of being throttled for power savings.",
            PossibleSideEffects = "Higher power draw and reduced battery life on laptops while active; restored automatically after Roblox closes.",
            RequiresAdmin = false,
            Priority = priority + 3,
            IsAutomatable = true
        });

        // --- Hardware upgrade guidance (only when measurements indicate a real bottleneck) ---
        var ramPercent = snapshot.System.RamUsagePercent;
        var ramTotalMb = snapshot.System.RamTotalMegabytes;
        if (ramPercent.IsAvailable && ramPercent.Value > 90 && ramTotalMb.IsAvailable && ramTotalMb.Value < 8500)
        {
            recommendations.Add(new Recommendation
            {
                ActionId = null,
                Cause = LagCause.BackgroundApplications,
                Title = "Consider a RAM upgrade",
                DetectedProblem = $"RAM usage measured at {ramPercent.Value:0}% of only {ramTotalMb.Value / 1024.0:0.#} GB total, even after closing background apps.",
                ProposedChange = "Consider upgrading to more system RAM (8 GB is tight for modern Windows + a browser + Roblox together).",
                ExpectedBenefit = "More RAM reduces disk paging/stuttering caused by running out of memory.",
                PossibleSideEffects = "Requires purchasing and installing hardware - only suggested because measured usage indicates a real bottleneck, not as a default recommendation.",
                RequiresAdmin = false,
                Priority = priority + 20,
                IsAutomatable = false
            });
        }

        var gpuUsage = snapshot.System.GpuUsagePercent;
        if (fpsStatus is StatusLevel.Warning or StatusLevel.Critical && gpuUsage.IsAvailable && gpuUsage.Value > 95)
        {
            recommendations.Add(new Recommendation
            {
                ActionId = null,
                Cause = LagCause.LowFps,
                Title = "GPU is fully utilized - consider a GPU upgrade",
                DetectedProblem = $"GPU usage measured at {gpuUsage.Value:0}% while FPS remains low ({fps.Value:0.#} FPS), even at reduced graphics settings.",
                ProposedChange = "This is a hardware ceiling for the experiences you're playing; a faster GPU would raise the achievable frame rate.",
                ExpectedBenefit = "A faster GPU raises your FPS ceiling in demanding experiences.",
                PossibleSideEffects = "Requires purchasing and installing hardware - only suggested because measurements indicate the GPU itself is the bottleneck.",
                RequiresAdmin = false,
                Priority = priority + 21,
                IsAutomatable = false
            });
        }

        var orderedRecommendations = recommendations
            .OrderBy(r => r.Priority)
            .ToList();

        return new DiagnosticsResult
        {
            DetectedCauses = causes,
            LagExplanation = lagExplanation,
            Recommendations = orderedRecommendations
        };
    }
}
