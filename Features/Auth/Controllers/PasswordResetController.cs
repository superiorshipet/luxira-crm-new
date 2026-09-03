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
        var callback = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(token)}";
        await emailService.SendEmailAsync(user.Email ?? request.Email.Trim(), "Reset Password", $"<p>Please reset your password by <a href='{System.Net.WebUtility.HtmlEncode(callback)}'>clicking here</a>.</p>", ct: ct);
        return Ok(new { success = true });
    }

    [AllowAnonymous]
    [HttpPost("/api/auth/reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.NewPassword.Length < 8) return BadRequest(new { success = false, message = "Invalid token or password." });
        PasswordResetPayload payload;
        try
        {
            var protectedText = System.Text.Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
            payload = JsonSerializer.Deserialize<PasswordResetPayload>(_protector.Unprotect(protectedText)) ?? throw new InvalidOperationException();
        }
        catch { return BadRequest(new { success = false, message = "Invalid or expired reset token." }); }
        if (payload.ExpiresAt < DateTimeOffset.UtcNow) return BadRequest(new { success = false, message = "Invalid or expired reset token." });
        var user = await context.Users.FirstOrDefaultAsync(item => item.Id == payload.UserId, ct);
        if (user is null || !string.Equals(user.SecurityStamp, payload.SecurityStamp, StringComparison.Ordinal)) return BadRequest(new { success = false, message = "Invalid or expired reset token." });
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}

public sealed record MobileForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
internal sealed record PasswordResetPayload(string UserId, string? SecurityStamp, DateTimeOffset ExpiresAt);
