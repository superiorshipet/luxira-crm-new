using System.Security.Claims;
using Luxira.Api.Data;
using Luxira.Api.Features.Auth.DTOs;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Features.Auth.Repositories;
using Luxira.Api.Features.Auth.Services;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Auth.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/auth/switch")]
[Route("AccountSwitch")]
public sealed class AccountSwitchController : ControllerBase
{
    private const string OriginalAdminUserIdClaim = "OriginalAdminUserId";
    private const string OriginalAdminEmailClaim = "OriginalAdminEmail";
    private const string OriginalAdminNameClaim = "OriginalAdminName";
    private const string IsAdminSwitchSessionClaim = "IsAdminSwitchSession";
    private const string IsSwitchedAccountClaim = "IsSwitchedAccount";

    private readonly ApplicationDbContext _context;
    private readonly UserRepository _users;
    private readonly JwtService _jwtService;
    private readonly AuthCookieService _authCookieService;
    private readonly ILogger<AccountSwitchController> _logger;

    public AccountSwitchController(
        ApplicationDbContext context,
        UserRepository users,
        JwtService jwtService,
        AuthCookieService authCookieService,
        ILogger<AccountSwitchController> logger)
    {
        _context = context;
        _users = users;
        _jwtService = jwtService;
        _authCookieService = authCookieService;
        _logger = logger;
    }

    [HttpGet("available")]
    [HttpGet("GetAvailableAccounts")]
    [HttpGet("MyAccounts")]
    public async Task<IActionResult> MyAccounts(CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId) || !CanUseAccountSwitch())
        {
            return Unauthorized(new { success = false, message = "غير مسموح باستخدام تبديل الحسابات." });
        }

        var employeeAccounts = await (
            from employee in _context.Employees.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on employee.ApplicationUserId equals user.Id
            where employee.ApplicationUserId != null &&
                  employee.ApplicationUserId != "" &&
                  user.Email != null &&
                  user.Email != ""
            select new SwitchAccountRow(
                user.Id,
                employee.Id,
                user.Email!,
                user.UserName,
                FirstNonEmpty(employee.DisplayName, employee.Name, user.Name, user.Email),
                employee.IsActive))
            .ToListAsync(ct);

        var employeeUserIds = employeeAccounts
            .Select(account => account.Id)
            .ToHashSet(StringComparer.Ordinal);

        var deliveryUserIds = await (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where role.Name == "DeliveryCompany" || role.Name == "DeliveryRepresentative"
            select userRole.UserId)
            .Distinct()
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var deliveryAccounts = await _context.Users
            .AsNoTracking()
            .Where(user => deliveryUserIds.Contains(user.Id) &&
                           !employeeUserIds.Contains(user.Id) &&
                           user.Email != null &&
                           user.Email != "")
            .Select(user => new SwitchAccountRow(
                user.Id,
                0,
                user.Email!,
                user.UserName,
                user.Name ?? user.Email!,
                !user.LockoutEnd.HasValue || user.LockoutEnd <= now))
            .ToListAsync(ct);

        var accounts = employeeAccounts.Concat(deliveryAccounts).ToList();
        var accountIds = accounts.Select(account => account.Id).ToList();
        var roleRows = await (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where accountIds.Contains(userRole.UserId)
            select new { userRole.UserId, role.Name })
            .ToListAsync(ct);
        var rolesByUser = roleRows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => string.Join("، ", group.Select(row => row.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct()),
                StringComparer.Ordinal);

        var result = accounts
            .GroupBy(account => account.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(account => account.DisplayName)
            .ThenBy(account => account.Email)
            .Select(account => new
            {
                id = account.Id,
                employeeId = account.EmployeeId,
                email = account.Email,
                userName = account.UserName,
                displayName = account.DisplayName,
                roleName = rolesByUser.GetValueOrDefault(account.Id, string.Empty),
                isActive = account.IsActive,
                isCurrent = account.Id == currentUserId,
            });

        return Ok(new
        {
            success = true,
            title = "الملفات الشخصية للموظفين",
            accounts = result,
        });
    }

    [HttpPost("switch-user")]
    [HttpPost("SwitchUser")]
    public async Task<IActionResult> SwitchUser(
        [FromBody] SwitchUserRequest request,
        CancellationToken ct)
    {
        var switched = await SwitchCoreAsync(request.TargetUserId, ct);
        if (switched.Error is not null) return switched.Error;

        await _authCookieService.SignInTokenAsync(HttpContext, switched.Response!.Token);
        return Ok(switched.Response);
    }

    [HttpPost("Switch")]
    public async Task<IActionResult> Switch(
        [FromBody] SwitchUserRequest request,
        CancellationToken ct)
    {
        var switched = await SwitchCoreAsync(request.TargetUserId, ct);
        if (switched.Error is not null) return switched.Error;

        await _authCookieService.SignInTokenAsync(HttpContext, switched.Response!.Token);
        return Ok(new
        {
            success = true,
            message = "تم فتح الملف الشخصي للموظف بنجاح.",
            redirectUrl = "/Home/Index",
        });
    }

    [HttpPost("ReturnToOriginalAdmin")]
    public async Task<IActionResult> ReturnToOriginalAdmin(CancellationToken ct)
    {
        var originalAdmin = await ResolveOriginalAdminAsync(ct);
        if (originalAdmin is null)
        {
            return BadRequest(new { success = false, message = "هذه الجلسة ليست جلسة سويتش من أدمن." });
        }

        var response = CreateAuthResponse(originalAdmin);
        await _authCookieService.SignInTokenAsync(HttpContext, response.Token);
        return Ok(new
        {
            success = true,
            message = "تم الرجوع لحساب الأدمن الأصلي.",
            redirectUrl = "/Home/Index",
        });
    }

    [HttpGet("ReturnToOriginalAdminDirect")]
    public async Task<IActionResult> ReturnToOriginalAdminDirect(CancellationToken ct)
    {
        var originalAdmin = await ResolveOriginalAdminAsync(ct);
        if (originalAdmin is null)
        {
            await _authCookieService.SignOutAsync(HttpContext);
            return Redirect("/Account/Login");
        }

        var response = CreateAuthResponse(originalAdmin);
        await _authCookieService.SignInTokenAsync(HttpContext, response.Token);
        return Redirect("/Home/Index");
    }

    [HttpGet("LogoutSwitchToLogin")]
    public async Task<IActionResult> LogoutSwitchToLogin()
    {
        var email = User.FindFirstValue(OriginalAdminEmailClaim) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(email))
        {
            Response.Cookies.Append(
                "LuxiraLoginPreferredEmail",
                email,
                new CookieOptions
                {
                    Path = "/",
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(10),
                });
        }

        await _authCookieService.SignOutAsync(HttpContext);
        return Redirect($"/Account/Login?selectedEmail={Uri.EscapeDataString(email)}");
    }

    private async Task<SwitchResult> SwitchCoreAsync(string? targetUserId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId) || !CanUseAccountSwitch())
        {
            return SwitchResult.Fail(Unauthorized(new { success = false, message = "غير مسموح باستخدام تبديل الحسابات." }));
        }

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return SwitchResult.Fail(BadRequest(new { success = false, message = "اختاري الملف الشخصي أولًا." }));
        }

        if (targetUserId == currentUserId)
        {
            return SwitchResult.Fail(BadRequest(new { success = false, message = "أنتِ بالفعل داخل هذا الحساب." }));
        }

        var target = await _users.GetByIdAsync(targetUserId, ct);
        if (target is null)
        {
            return SwitchResult.Fail(NotFound(new { success = false, message = "الحساب غير موجود." }));
        }

        var isEmployee = await _context.Employees
            .AsNoTracking()
            .AnyAsync(employee => employee.ApplicationUserId == targetUserId, ct);
        var isDeliveryAccount = target.Roles.Any(role =>
            role is "DeliveryCompany" or "DeliveryRepresentative");
        if (!isEmployee && !isDeliveryAccount)
        {
            return SwitchResult.Fail(BadRequest(new
            {
                success = false,
                message = "هذا الحساب غير مربوط بموظف أو شركة توصيل أو مندوب، لذلك لن يظهر كملف شخصي للسويتش.",
            }));
        }

        var originalAdmin = await ResolveOriginalAdminAsync(ct, currentUserId);
        if (originalAdmin is null)
        {
            return SwitchResult.Fail(Unauthorized(new { success = false, message = "غير مسموح باستخدام تبديل الحسابات." }));
        }

        var additionalClaims = new[]
        {
            new Claim(IsAdminSwitchSessionClaim, "true"),
            new Claim(IsSwitchedAccountClaim, "true"),
            new Claim("BypassEmployeeLoginBlock", "true"),
            new Claim("BypassScreenRecording", "true"),
            new Claim("BypassCheckInFaceCapture", "true"),
            new Claim("BypassCheckOutFaceCapture", "true"),
            new Claim(OriginalAdminUserIdClaim, originalAdmin.Id),
            new Claim(OriginalAdminEmailClaim, originalAdmin.Email ?? originalAdmin.UserName ?? string.Empty),
            new Claim(OriginalAdminNameClaim, FirstNonEmpty(originalAdmin.Name, originalAdmin.Email, originalAdmin.UserName)),
        };

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Admin account switch from {CurrentUserId} to {TargetUserId}, original admin {OriginalAdminUserId}",
                currentUserId,
                targetUserId,
                originalAdmin.Id);
        }

        return SwitchResult.Success(CreateAuthResponse(target, additionalClaims));
    }

    private bool CanUseAccountSwitch() =>
        User.IsInRole("Admin") ||
        User.IsInRole("Administrator") ||
        User.FindFirstValue(IsAdminSwitchSessionClaim) == "true";

    private async Task<ApplicationUser?> ResolveOriginalAdminAsync(
        CancellationToken ct,
        string? currentUserId = null)
    {
        var originalAdminId = User.FindFirstValue(OriginalAdminUserIdClaim);
        if (string.IsNullOrWhiteSpace(originalAdminId) &&
            (User.IsInRole("Admin") || User.IsInRole("Administrator")))
        {
            originalAdminId = currentUserId ?? User.GetUserId();
        }

        if (string.IsNullOrWhiteSpace(originalAdminId)) return null;

        var user = await _users.GetByIdAsync(originalAdminId, ct);
        return user?.Roles.Any(role => role is "Admin" or "Administrator") == true
            ? user
            : null;
    }

    private AuthResponse CreateAuthResponse(
        ApplicationUser user,
        IEnumerable<Claim>? additionalClaims = null)
    {
        var (token, expiresAt) = _jwtService.GenerateToken(user, additionalClaims);
        return new AuthResponse(
            token,
            Guid.NewGuid().ToString(),
            expiresAt,
            new UserDto(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email,
                user.Name,
                user.Country,
                user.AcessId,
                user.Role,
                user.IsActive));
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed record SwitchAccountRow(
        string Id,
        int EmployeeId,
        string Email,
        string? UserName,
        string DisplayName,
        bool IsActive);

    private sealed record SwitchResult(AuthResponse? Response, IActionResult? Error)
    {
        public static SwitchResult Success(AuthResponse response) => new(response, null);
        public static SwitchResult Fail(IActionResult error) => new(null, error);
    }
}
