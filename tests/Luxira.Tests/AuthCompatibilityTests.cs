using Luxira.Api.Features.Auth.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Luxira.Tests;

public sealed class AuthCompatibilityTests
{
    [Theory]
    [InlineData(nameof(AccountSwitchController.MyAccounts), "GET", "MyAccounts")]
    [InlineData(nameof(AccountSwitchController.Switch), "POST", "Switch")]
    [InlineData(nameof(AccountSwitchController.ReturnToOriginalAdmin), "POST", "ReturnToOriginalAdmin")]
    [InlineData(nameof(AccountSwitchController.ReturnToOriginalAdminDirect), "GET", "ReturnToOriginalAdminDirect")]
    [InlineData(nameof(AccountSwitchController.LogoutSwitchToLogin), "GET", "LogoutSwitchToLogin")]
    public void AccountSwitch_PreservesLegacyRoutes(
        string actionName,
        string httpMethod,
        string template)
    {
        var action = typeof(AccountSwitchController).GetMethod(actionName);

        Assert.NotNull(action);
        Assert.Contains(
            action.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>(),
            attribute =>
                attribute.HttpMethods.Contains(httpMethod) &&
                string.Equals(attribute.Template, template, StringComparison.Ordinal));
    }

    [Fact]
    public void AccountSwitch_RemainsAuthenticatedButNotAdminRoleLocked()
    {
        var controller = typeof(AccountSwitchController);
        var authorize = Assert.Single(
            controller.GetCustomAttributes(inherit: true).OfType<AuthorizeAttribute>());

        Assert.Null(authorize.Roles);
        Assert.All(
            controller.GetMethods()
                .Where(method => method.DeclaringType == controller),
            method => Assert.Empty(
                method.GetCustomAttributes(inherit: true).OfType<AllowAnonymousAttribute>()));
    }
}
