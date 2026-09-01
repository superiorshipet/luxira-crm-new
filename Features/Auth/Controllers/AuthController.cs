using System.Security.Claims;
using Luxira.Api.Features.Auth.DTOs;
using Luxira.Api.Features.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Auth.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [HttpPost("/Account/Login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var response = await _authService.LoginAsync(request, ct);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var response = await _authService.RegisterAsync(request, ct);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("switch-user")]
    [HttpPost("/AccountSwitch/SwitchUser")]
    public async Task<ActionResult<AuthResponse>> SwitchUser([FromBody] SwitchUserRequest request, CancellationToken ct)
    {
        var currentUserId = Luxira.Api.Utils.Extensions.ClaimsPrincipalExtensions.GetUserId(User);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        var response = await _authService.SwitchUserAsync(currentUserId, request.TargetUserId, ct);
        return Ok(response);
    }
}
