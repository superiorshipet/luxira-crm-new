using Luxira.Api.Features.Auth.DTOs;
using Luxira.Api.Features.Auth.Repositories;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Features.Auth.Services;

public class UserService
{
    private readonly UserRepository _userRepository;

    public UserService(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserProfileResponse> GetProfileAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }

        return new UserProfileResponse(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email,
            user.Name,
            user.Country,
            user.AcessId,
            user.Role,
            "SqlServer"
        );
    }

    public async Task<List<UserDto>> GetAllActiveUsersAsync(CancellationToken ct = default)
    {
        var users = await _userRepository.GetAllActiveAsync(ct);
        return users.Select(u => new UserDto(
            u.Id,
            u.UserName ?? string.Empty,
            u.Email,
            u.Name,
            u.Country,
            u.AcessId,
            u.Role,
            u.IsActive
        )).ToList();
    }
}
