using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.Identity.GetUserProfile;

namespace Luxira.Infrastructure.Features.Identity.GetUserProfile;

internal sealed class UnavailableGetUserProfileRepository
    : IGetUserProfileRepository
{
    public Task<UserProfileSource?> GetAsync(
        string userId,
        CancellationToken cancellationToken) =>
        throw new ReadStoreUnavailableException(
            "The isolated SQL read infrastructure is not configured in this environment.");
}
