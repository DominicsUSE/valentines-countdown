namespace RobloxOptimizer.RobloxIntegration;

/// <summary>Static, plain-language reference text shown in the UI. No automation reads or changes any of this inside Roblox.</summary>
public static class RobloxGuidance
{
    public const string EnablePerformanceStats =
        "While playing, press Shift+F5 (or Shift+Ctrl+F5 depending on your keyboard layout) to toggle Roblox's own Performance Stats overlay. " +
        "It shows Roblox's authoritative FPS, ping, and memory numbers as reported by the game client itself - use it to double-check anything RPO shows you.";

    public const string PerformanceVariesNote =
        "Performance depends heavily on which Roblox experience you're playing and which server you're matched to. A place with complex scripting or " +
        "many other players will run slower than an empty, simple one even on identical hardware and network conditions.";

    public const string NoFastFlagsNote =
        "RPO never modifies Roblox's installation files, client settings, or FastFlags (documented or undocumented), and never automates input into " +
        "the game. All Roblox-side changes above are instructions for you to apply yourself from Roblox's own Settings menu.";
}
