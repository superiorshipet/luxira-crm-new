using Microsoft.AspNetCore.Http.HttpResults;

namespace Luxira.Api.Features.ReferenceData.FailureReasons;

internal static class FailureReasonEndpoints
{
    private static readonly FailureReasonResponse[] All =
    [
        new(1, "لم يرد على الهاتف"),
        new(2, "رقم الهاتف مغلق"),
        new(3, "رقم الهاتف غير فعال"),
        new(4, "رفض استلام الطلب"),
        new(5, "العميل غير متاح للاستلام"),
        new(6, "العميل مسافر"),
        new(7, "رفض بسبب عدم الفحص"),
        new(8, "المبلغ المطلوب غير متوفر"),
        new(9, "تأجيل الاستلام"),
        new(10, "خطأ بالطلبية"),
        new(11, "الطلب غير مطابق للمطلوب"),
    ];

    internal static IEndpointRouteBuilder MapFailureReasonEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var publicEndpoints = endpoints
            .MapGroup(string.Empty)
            .AllowAnonymous();

        publicEndpoints.MapGet(
                "/api/v1/reference-data/failure-reasons",
                GetFailureReasons)
            .WithName("ReferenceData_GetFailureReasons")
            .WithTags("Reference Data")
            .WithSummary("List delivery failure reasons")
            .CacheOutput("ReferenceData")
            .Produces<FailureReasonResponse[]>();

        publicEndpoints.MapGet(
                "/DataList/GetAllFailureReasons",
                GetFailureReasons)
            .WithName("LegacyDataList_GetAllFailureReasons")
            .WithTags("Legacy Compatibility")
            .WithSummary("List delivery failure reasons using the legacy route")
            .CacheOutput("ReferenceData")
            .Produces<FailureReasonResponse[]>();

        return endpoints;
    }

    private static Ok<FailureReasonResponse[]> GetFailureReasons() =>
        TypedResults.Ok(All);
}

internal sealed record FailureReasonResponse(
    int Id,
    string Name);
