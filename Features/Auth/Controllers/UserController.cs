using Luxira.Api.Features.Auth.DTOs;
using Luxira.Api.Features.Auth.Services;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Auth.Controllers;

[ApiController]
[Route("api/v1/users")]
[Route("api/v1/user")]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly UserService _userService;

    public UserController(UserService userService)
    {
        _userService = userService;
    }

    [Authorize]
    [HttpGet("me")]
    [HttpGet("/api/v1/identity/profile")]
    [HttpGet("/Account/GetProfile")]
    public async Task<ActionResult<UserProfileResponse>> GetCurrentUser(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var profile = await _userService.GetProfileAsync(userId, ct);
        return Ok(profile);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfileResponse>> GetUserById(string id, CancellationToken ct)
    {
        var profile = await _userService.GetProfileAsync(id, ct);
        return Ok(profile);
    }

    [Authorize]
    [HttpGet("active")]
    public async Task<ActionResult<List<UserDto>>> GetActiveUsers(CancellationToken ct)
    {
        var users = await _userService.GetAllActiveUsersAsync(ct);
        return Ok(users);
    }
}
