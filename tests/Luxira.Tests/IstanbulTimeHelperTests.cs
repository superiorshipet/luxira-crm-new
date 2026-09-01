using Luxira.Api.Utils.Time;
using Xunit;

namespace Luxira.Tests;

public class IstanbulTimeHelperTests
{
    [Fact]
    public void IstanbulTimeHelper_Now_ShouldBeUtcPlus3()
    {
        var nowUtc = DateTime.UtcNow;
        var istanbulNow = IstanbulTimeHelper.Now;

        // Istanbul is UTC+3 throughout the year
        var diff = istanbulNow - nowUtc;
        Assert.True(Math.Abs(diff.TotalHours - 3) < 0.1, $"Expected ~3 hours difference from UTC, got {diff.TotalHours}");
    }
}
