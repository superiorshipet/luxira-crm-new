using Luxira.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Luxira.Api.Errors;

internal static class ReadStoreProblem
{
    internal static ProblemHttpResult Create(
        ReadStoreUnavailableException exception) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Read infrastructure unavailable",
            detail: exception.Message);
}
