using RobloxOptimizer.Network;
using Xunit;

namespace RobloxOptimizer.Tests;

public class WifiSignalReaderParsingTests
{
    private const string SampleConnectedOutput = """
        There is 1 interface on the system:

            Name                   : Wi-Fi
            Description             : Intel(R) Wi-Fi 6 AX201 160MHz
            GUID                    : 12345678-1234-1234-1234-123456789012
            Physical address        : ab:cd:ef:12:34:56
            State                   : connected
            SSID                    : HomeNetwork
            BSSID                   : 12:34:56:78:9a:bc
            Network type            : Infrastructure
            Radio type               : 802.11ac
            Authentication           : WPA2-Personal
            Cipher                   : CCMP
            Connection mode          : Auto Connect
            Channel                  : 44
            Receive rate (Mbps)      : 866.7
            Transmit rate (Mbps)     : 866.7
            Signal                  : 84%
            Profile                  : HomeNetwork
        """;

    private const string SampleDisconnectedOutput = """
        There is 1 interface on the system:

            Name                   : Wi-Fi
            Description             : Intel(R) Wi-Fi 6 AX201 160MHz
            GUID                    : 12345678-1234-1234-1234-123456789012
            State                   : disconnected
        """;

    [Fact]
    public void ParseSignalPercent_ExtractsValueFromConnectedInterface()
    {
        var result = WifiSignalReader.ParseSignalPercent(SampleConnectedOutput);

        Assert.True(result.IsAvailable);
        Assert.Equal(84, result.Value);
    }

    [Fact]
    public void ParseSignalPercent_ReportsUnavailable_WhenDisconnected()
    {
        var result = WifiSignalReader.ParseSignalPercent(SampleDisconnectedOutput);

        Assert.False(result.IsAvailable);
        Assert.NotNull(result.UnavailableReason);
    }

    [Fact]
    public void ParseSignalPercent_NeverThrows_OnEmptyOrGarbageInput()
    {
        var result = WifiSignalReader.ParseSignalPercent(string.Empty);

        Assert.False(result.IsAvailable);
    }
}
