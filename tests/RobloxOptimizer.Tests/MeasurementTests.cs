using RobloxOptimizer.Core.Models;
using Xunit;

namespace RobloxOptimizer.Tests;

public class MeasurementTests
{
    [Fact]
    public void Of_MarksValueAvailable()
    {
        var measurement = Measurement<double>.Of(42.5);

        Assert.True(measurement.IsAvailable);
        Assert.Equal(42.5, measurement.Value);
        Assert.Null(measurement.UnavailableReason);
    }

    [Fact]
    public void Unavailable_NeverExposesAValue()
    {
        var measurement = Measurement<double>.Unavailable("sensor not supported");

        Assert.False(measurement.IsAvailable);
        Assert.Equal(default, measurement.Value);
        Assert.Equal("sensor not supported", measurement.UnavailableReason);
    }

    [Fact]
    public void ToString_ShowsUnavailableReason_WhenNotAvailable()
    {
        var measurement = Measurement<int>.Unavailable("no firmware support");

        Assert.Contains("Unavailable", measurement.ToString());
        Assert.Contains("no firmware support", measurement.ToString());
    }
}
