using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Luxira.Api.Errors;
using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.Identity.GetUserProfile;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Identity.GetUserProfile;

internal static class UserProfileController
{
    internal static IEndpointRouteBuilder MapUserProfileController(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/users/{id}/profile",
                GetById)
            .WithName("Identity_GetUserProfile")
            .WithTags("Identity and Access")
            .WithSummary("Get an authenticated user's profile")
            .Produces<UserProfileResult>()
            .Produces<UserProfileErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<UserProfileErrorResponse>(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet(
                "/api/v1/users/me/profile",
                GetCurrent)
            .WithName("Identity_GetCurrentUserProfile")
            .WithTags("Identity and Access")
            .WithSummary("Get the current JWT user's profile")
            .Produces<UserProfileResult>()
            .Produces<UserProfileErrorResponse>(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet(
                "/Conference/UserProfile",
                GetLegacy)
            .WithName("LegacyConference_GetUserProfile")
            .WithTags("Legacy Compatibility")
            .WithSummary("Get a user profile using the legacy route")
            .Produces<UserProfileResult>()
            .Produces<UserProfileErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<UserProfileErrorResponse>(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static Task<IResult> GetById(
        string id,
        GetUserProfileService service,
        CancellationToken cancellationToken) =>
        GetProfile(id, service, cancellationToken);

    private static Task<IResult> GetLegacy(
        [FromQuery] string? id,
        GetUserProfileService service,
        CancellationToken cancellationToken) =>
        GetProfile(id, service, cancellationToken);

    private static Task<IResult> GetCurrent(
        ClaimsPrincipal user,
        GetUserProfileService service,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ??
            user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return GetProfile(userId, service, cancellationToken);
    }

    private static async Task<IResult> GetProfile(
        string? userId,
        GetUserProfileService service,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return TypedResults.BadRequest(
                new UserProfileErrorResponse("معرف المستخدم مطلوب."));
        }

        try
        {
            var result = await service.ExecuteAsync(userId, cancellationToken);
            return result is null
                ? TypedResults.NotFound(
                    new UserProfileErrorResponse("المستخدم غير موجود."))
                : TypedResults.Ok(result);
        }
        catch (ReadStoreUnavailableException exception)
        {
            return ReadStoreProblem.Create(exception);
        }
    }
}
