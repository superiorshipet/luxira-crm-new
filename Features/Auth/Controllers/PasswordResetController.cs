using System.Text.Json;
using Luxira.Api.Data;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Infrastructure.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;

namespace Luxira.Api.Features.Auth.Controllers;

[ApiController]
public sealed class PasswordResetController(
    ApplicationDbContext context,
    IDataProtectionProvider protectionProvider,
    IPasswordHasher<ApplicationUser> passwordHasher,
    LuxiraEmailService emailService,
    IConfiguration configuration) : ControllerBase
{
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("Luxira.PasswordReset.v1");

    [AllowAnonymous]
    [HttpPost("/Api/Auth/ForgotPassword")]
    [HttpPost("/api/auth/forgot-password")]
    public async Task<IActionResult> MobileForgotPassword([FromBody] MobileForgotPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest("Email is required.");
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await context.Users.FirstOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail || item.Email == request.Email.Trim(), ct);
        if (user is null) return Ok(new { success = true });
        var payload = JsonSerializer.Serialize(new PasswordResetPayload(user.Id, user.SecurityStamp, DateTimeOffset.UtcNow.AddMinutes(30)));
        var token = WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(_protector.Protect(payload)));
        var configuredBaseUrl = configuration["PublicBaseUrl"]?.TrimEnd('/');
        var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl) ? $"{Request.Scheme}://{Request.Host}" : configuredBaseUrl;
        var callbackPath = configuration["PasswordReset:CallbackPath"];
        if (string.IsNullOrWhiteSpace(callbackPath)) callbackPath = "/reset-password";
        if (!callbackPath.StartsWith('/')) callbackPath = "/" + callbackPath;
        var separator = callbackPath.Contains('?') ? '&' : '?';
        var callback = $"{baseUrl}{callbackPath}{separator}token={Uri.EscapeDataString(token)}";
        await emailService.SendEmailAsync(user.Email ?? request.Email.Trim(), "Reset Password", $"<p>Please reset your password by <a href='{System.Net.WebUtility.HtmlEncode(callback)}'>clicking here</a>.</p>", ct: ct);
        return Ok(new { success = true });
    }

    [AllowAnonymous]
    [HttpPost("/api/auth/reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var error = await ResetPasswordCore(request.Token, request.NewPassword, ct);
        return error is null
            ? Ok(new { success = true })
            : BadRequest(new { success = false, message = error });
    }

    [AllowAnonymous]
    [HttpGet("/reset-password")]
    [HttpGet("/Account/ResetPassword")]
    public IActionResult ResetPasswordPage([FromQuery] string? token, [FromQuery] string? code)
    {
        var resetToken = token ?? code;
        if (string.IsNullOrWhiteSpace(resetToken)) return BadRequest("Reset token is required.");
        var encodedToken = WebUtility.HtmlEncode(resetToken);
        const string policy = "Use at least 6 characters and one digit.";
        var html = $$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Reset password</title></head>
            <body><main><h1>Reset password</h1><form method="post" action="/Account/ResetPassword">
            <input type="hidden" name="token" value="{{encodedToken}}"><label>New password <input type="password" name="newPassword" minlength="6" required></label>
            <p>{{policy}}</p><button type="submit">Reset password</button></form></main></body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }

    [AllowAnonymous]
    [HttpPost("/Account/ResetPassword")]
    public async Task<IActionResult> ResetPasswordForm([FromForm] PasswordResetFormRequest request, CancellationToken ct)
    {
        var error = await ResetPasswordCore(request.Token, request.NewPassword, ct);
        var message = WebUtility.HtmlEncode(error ?? "Password reset successfully. You can sign in now.");
        return Content($"<!doctype html><html><body><main><p>{message}</p></main></body></html>", "text/html; charset=utf-8");
    }

    private async Task<string?> ResetPasswordCore(string? token, string? newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || !LuxiraPasswordPolicy.IsValid(newPassword))
            return LuxiraPasswordPolicy.ErrorMessage;
        PasswordResetPayload payload;
        try
        {
            var protectedText = System.Text.Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            payload = JsonSerializer.Deserialize<PasswordResetPayload>(_protector.Unprotect(protectedText)) ?? throw new InvalidOperationException();
        }
        catch { return "Invalid or expired reset token."; }
        if (payload.ExpiresAt < DateTimeOffset.UtcNow) return "Invalid or expired reset token.";
        var user = await context.Users.FirstOrDefaultAsync(item => item.Id == payload.UserId, ct);
        if (user is null || !string.Equals(user.SecurityStamp, payload.SecurityStamp, StringComparison.Ordinal)) return "Invalid or expired reset token.";
        user.PasswordHash = passwordHasher.HashPassword(user, newPassword!);
        user.SecurityStamp = Guid.NewGuid().ToString();
        await context.SaveChangesAsync(ct);
        return null;
    }
}

public sealed record MobileForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record PasswordResetFormRequest(string Token, string NewPassword);
internal sealed record PasswordResetPayload(string UserId, string? SecurityStamp, DateTimeOffset ExpiresAt);
