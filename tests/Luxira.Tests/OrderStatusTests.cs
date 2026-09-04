using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Utils.Exceptions;
using Xunit;

namespace Luxira.Tests;

public class OrderStatusTests
{
    [Theory]
    [InlineData(0, "طلب جديد")]
    [InlineData(4, "قيد التوصيل")]
    [InlineData(5, "تم التسليم المؤقت")]
    [InlineData(6, "تم التسليم")]
    [InlineData(7, "فشل التسليم")]
    [InlineData(8, "انتظار المعالجة")]
    [InlineData(9, "الطلبات المرجعة")]
    [InlineData(13, "تم تحديث الرصيد")]
    [InlineData(14, "تم الدفع")]
    public void OrderStatusCodes_ShouldMatchLegacyEnumValues(int statusCode, string expectedDisplayName)
    {
        Assert.True(OrderStatusCodes.IsDefined(statusCode));
        Assert.Equal(expectedDisplayName, OrderStatusCodes.GetDisplayName(statusCode));
    }

    [Fact]
    public void OrderStatusTransitionPolicy_ShouldEnforceFailureReason_ForFailureStatuses()
    {
        var policy = new OrderStatusTransitionPolicy();
        var order = new Order { Id = 1, OrderStatus = OrderStatusCodes.InDelivery };
        var actor = new OrderStatusActor("user1", new HashSet<string> { "Admin" });

        // When reason is missing on failure status, should throw BadRequestException
        Assert.Throws<BadRequestException>(() =>
            policy.EnsureAllowed(order, OrderStatusCodes.FailedDelivery, null, actor));
    }

    [Fact]
    public void OrderStatusTransitionPolicy_ShouldAllowValidTransition_ForAdmin()
    {
        var policy = new OrderStatusTransitionPolicy();
        var order = new Order { Id = 1, OrderStatus = OrderStatusCodes.InDelivery };
        var actor = new OrderStatusActor("user1", new HashSet<string> { "Admin" });

        // Valid transition to Delivered
        policy.EnsureAllowed(order, OrderStatusCodes.Delivered, null, actor);
    }

    [Fact]
    public void OrderStatusTransitionPolicy_ShouldAllowTrustedCourierFailureWithoutReason()
    {
        var policy = new OrderStatusTransitionPolicy();
        var order = new Order { Id = 1, OrderStatus = OrderStatusCodes.InDelivery };

        policy.EnsureAllowed(
            order,
            OrderStatusCodes.FailedDelivery,
            null,
            OrderStatusActor.TrustedSystem("courier-webhook"));
    }
}
