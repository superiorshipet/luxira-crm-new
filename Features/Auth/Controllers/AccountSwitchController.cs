using Luxira.Api.Features.Auth.DTOs;
using Luxira.Api.Features.Auth.Services;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Auth.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/auth/switch")]
[Route("AccountSwitch")]
public class AccountSwitchController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly UserService _userService;

    public AccountSwitchController(AuthService authService, UserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("switch-user")]
    [HttpPost("SwitchUser")]
    public async Task<ActionResult<AuthResponse>> SwitchUser([FromBody] SwitchUserRequest request, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        var response = await _authService.SwitchUserAsync(currentUserId, request.TargetUserId, ct);
        return Ok(response);
    }

    [HttpGet("available")]
    [HttpGet("GetAvailableAccounts")]
    public async Task<ActionResult<List<UserDto>>> GetAvailableAccounts(CancellationToken ct)
    {
        var users = await _userService.GetAllActiveUsersAsync(ct);
        return Ok(users);
    }
}
