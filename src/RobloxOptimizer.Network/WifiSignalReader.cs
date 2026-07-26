using System.Diagnostics;
using System.Text.RegularExpressions;
using RobloxOptimizer.Core.Models;

namespace RobloxOptimizer.Network;

/// <summary>
/// Reads the current Wi-Fi signal strength by parsing the output of the
/// built-in <c>netsh wlan show interfaces</c> command - the same data
/// Windows' own network flyout uses. No third-party Wi-Fi library is used.
///
/// Known limitation: <c>netsh</c> prints its field names in the OS display
/// language, so this parser only recognizes the English "Signal" label. On
/// a non-English Windows install the signal will be reported as
/// unavailable rather than silently wrong.
/// </summary>
public static class WifiSignalReader
{
    private static readonly Regex SignalLineRegex = new(@"^\s*Signal\s*:?\s*(\d{1,3})\s*%\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    public static async Task<Measurement<int>> GetSignalPercentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Measurement<int>.Unavailable("Could not start netsh");
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return ParseSignalPercent(output);
        }
        catch (Exception ex)
        {
            return Measurement<int>.Unavailable("Wi-Fi signal read failed: " + ex.Message);
        }
    }

    /// <summary>Pure parsing logic, separated from process execution so it can be unit tested against sample netsh output.</summary>
    public static Measurement<int> ParseSignalPercent(string netshOutput)
    {
        var match = SignalLineRegex.Match(netshOutput);
        if (!match.Success)
        {
            return Measurement<int>.Unavailable("No active Wi-Fi interface found, or netsh output is in a non-English display language");
        }

        var percent = int.Parse(match.Groups[1].Value);
        return Measurement<int>.Of(Math.Clamp(percent, 0, 100));
    }
}
