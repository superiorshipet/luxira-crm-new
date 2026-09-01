using Luxira.Api.Infrastructure.Webhooks;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Luxira.Tests;

public sealed class WebhookSecurityTests
{
    [Fact]
    public void ValidateSharedSecret_AcceptsConfiguredSecret()
    {
        var security = CreateSecurity("expected-secret");
        var request = new DefaultHttpContext().Request;
        request.Headers["X-Luxira-Webhook-Secret"] = "expected-secret";

        security.ValidateSharedSecret(request, "Camex");
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong-secret")]
    public void ValidateSharedSecret_RejectsMissingOrWrongSecret(string supplied)
    {
        var security = CreateSecurity("expected-secret");
        var request = new DefaultHttpContext().Request;
        if (supplied.Length > 0) request.Headers["X-Luxira-Webhook-Secret"] = supplied;

        Assert.Throws<UnauthorizedException>(() =>
            security.ValidateSharedSecret(request, "Camex"));
    }

    [Fact]
    public void ValidateSharedSecret_FailsClosedWhenProviderIsNotConfigured()
    {
        var security = CreateSecurity(null);
        var request = new DefaultHttpContext().Request;

        Assert.Throws<InvalidOperationException>(() =>
            security.ValidateSharedSecret(request, "Camex"));
    }

    private static WebhookSecurity CreateSecurity(string? secret)
    {
        var values = secret is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { ["Webhooks:Camex:Secret"] = secret };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new WebhookSecurity(configuration, null!);
    }
}
