using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Luxira.Api.Data;
using Luxira.Api.Features.DeliveryCompanies.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Luxira.Tests;

public sealed class CourierHttpIntegrationTests
{
    [Fact]
    public async Task SandoogDetailsRefreshesRejectedTokenAndParsesFailureDetails()
    {
        var handler = new ScriptedHandler(
            Json(HttpStatusCode.OK, """{"access_token":"token-1","expires_in":3600}"""),
            Json(HttpStatusCode.Unauthorized, "{}"),
            Json(HttpStatusCode.OK, """{"access_token":"token-2","expires_in":3600}"""),
            Json(HttpStatusCode.OK, """[{"status":"Returned","reason":"No answer","reason_code":"NA","delivery_label":"https://label.test/1","fulfillment_id":"ful_1"}]"""));
        await using var fixture = CreateService(handler);

        var result = await fixture.Service.GetSandoogOrderDetailsAsync("ord_1", "Returned", default);

        Assert.True(result.Success);
        Assert.Equal("No answer", result.Reason);
        Assert.Equal("NA", result.ReasonCode);
        Assert.Equal("ful_1", result.FulfillmentId);
        Assert.Equal(2, handler.Requests.Count(request => request.Path == "/auth"));
        Assert.Equal(["token-1", "token-2"], handler.Requests.Where(request => request.Path.StartsWith("/orders/", StringComparison.Ordinal)).Select(request => request.Bearer));
    }

    [Fact]
    public async Task CamexTrackingRefreshesRejectedTokenAndParsesNumericState()
    {
        var validTo = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O");
        var handler = new ScriptedHandler(
            Json(HttpStatusCode.OK, "{\"type\":1,\"content\":{\"value\":\"token-1\",\"validTo\":\"" + validTo + "\"}}"),
            Json(HttpStatusCode.Unauthorized, "{}"),
            Json(HttpStatusCode.OK, "{\"type\":1,\"content\":{\"value\":\"token-2\",\"validTo\":\"" + validTo + "\"}}"),
            Json(HttpStatusCode.OK, """{"type":1,"content":6}"""));
        await using var fixture = CreateService(handler);

        var state = await fixture.Service.GetCamexStateAsync(12345, default);

        Assert.Equal(6, state);
        Assert.Equal(2, handler.Requests.Count(request => request.Path.StartsWith("/ApiEndpoints/Login", StringComparison.Ordinal)));
        Assert.Equal(["token-1", "token-2"], handler.Requests.Where(request => request.Path.StartsWith("/ApiEndpoints/TrackState", StringComparison.Ordinal)).Select(request => request.Bearer));
    }

    private static ServiceFixture CreateService(HttpMessageHandler handler)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"courier-http-{Guid.NewGuid():N}")
            .Options;
        var context = new ApplicationDbContext(options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sandoog:Enabled"] = "true",
            ["Sandoog:BaseUrl"] = "https://sandoog.test",
            ["Sandoog:ApiKey"] = "api-key",
            ["Sandoog:EntityId"] = "entity",
            ["Camex:Enabled"] = "true",
            ["Camex:BaseUrl"] = "https://camex.test",
            ["Camex:ProviderKey"] = "provider",
            ["Camex:ClientKey"] = "client"
        }).Build();
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        var factory = new HandlerHttpClientFactory(handler);
        var service = new CourierDispatchService(context, configuration, factory, cache, NullLogger<CourierDispatchService>.Instance);
        return new ServiceFixture(service, context, cache);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed record CapturedRequest(string Path, string? Bearer);

    private sealed class ScriptedHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            Assert.NotEmpty(_responses);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class HandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class ServiceFixture(CourierDispatchService service, ApplicationDbContext context, MemoryCache cache) : IAsyncDisposable
    {
        public CourierDispatchService Service { get; } = service;
        public async ValueTask DisposeAsync()
        {
            cache.Dispose();
            await context.DisposeAsync();
        }
    }
}
