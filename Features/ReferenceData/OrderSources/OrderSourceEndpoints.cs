using Microsoft.AspNetCore.Http.HttpResults;

namespace Luxira.Api.Features.ReferenceData.OrderSources;

internal static class OrderSourceEndpoints
{
    private static readonly OrderSourceResponse[] All =
    [
        new(1, "فيسبوك", "/socialmediaicons/facebook.svg"),
        new(2, "انستغرام", "/socialmediaicons/instagram.svg"),
        new(3, "سناب_شات", "/socialmediaicons/snapchat.svg"),
        new(4, "واتساب", "/socialmediaicons/whatsapp.svg"),
        new(5, "تك_توك", "/socialmediaicons/tiktok.svg"),
        new(6, "توتير", "/socialmediaicons/twitter.svg"),
        new(7, "ويبسات", "/socialmediaicons/internet.svg"),
        new(8, "اتصال", "/socialmediaicons/phone.svg"),
        new(9, "تلغرام", "/socialmediaicons/telegram.svg"),
        new(10, "ميتا", "/socialmediaicons/meta.svg"),
    ];

    internal static IEndpointRouteBuilder MapOrderSourceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/reference-data/order-sources",
                GetOrderSources)
            .WithName("ReferenceData_GetOrderSources")
            .WithTags("Reference Data")
            .WithSummary("List order acquisition sources")
            .CacheOutput("ReferenceData")
            .Produces<OrderSourceResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapGet(
                "/DataList/GetAllOrderSources",
                GetOrderSources)
            .WithName("LegacyDataList_GetAllOrderSources")
            .WithTags("Legacy Compatibility")
            .WithSummary("List order sources using the authenticated legacy route")
            .CacheOutput("ReferenceData")
            .Produces<OrderSourceResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static Ok<OrderSourceResponse[]> GetOrderSources() =>
        TypedResults.Ok(All);
}

internal sealed record OrderSourceResponse(
    int Id,
    string Name,
    string LogoUrl);
