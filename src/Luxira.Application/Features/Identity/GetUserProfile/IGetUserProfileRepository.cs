namespace Luxira.Application.Features.Identity.GetUserProfile;

public interface IGetUserProfileRepository
{
    Task<UserProfileSource?> GetAsync(
        string userId,
        CancellationToken cancellationToken);
}
