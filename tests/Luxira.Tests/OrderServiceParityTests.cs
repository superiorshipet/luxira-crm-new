using System.Reflection;
using Luxira.Api.Features.Orders.Services;

namespace Luxira.Tests;

public sealed class OrderServiceParityTests
{
    [Fact]
    public void TurkeyUsesLegacyCountryId()
    {
        var field = typeof(OrderService).GetField("TurkeyCountryId", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.Equal(7, field!.GetRawConstantValue());
    }

    [Theory]
    [InlineData("+90 555-123-4567", 7, "05551234567")]
    [InlineData("00971 50 123 4567", 2, "0501234567")]
    [InlineData("+974 5512 3456", 3, "55123456")]
    [InlineData("٠٥٥٥-١٢٣-٤٥٦٧", 7, "05551234567")]
    public void PhoneNormalizationMatchesLegacyLocalStorage(string value, int country, string expected)
    {
        var method = typeof(OrderService).GetMethod("NormalizePhoneNumber", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(expected, method!.Invoke(null, [value, country]));
    }
}
