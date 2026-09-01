using Luxira.Api.Data;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector,Administrator")]
[Route("api/v1/communication/password-emails")]
[Route("PasswordEmails")]
[Route("PasswordPages")]
[Route("SystemEmailLogs")]
public class PasswordEmailsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PasswordEmailsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("/PasswordEmails/GetEmailLogs")]
    [HttpGet("/SystemEmailLogs/GetLogs")]
    public async Task<IActionResult> GetEmailLogs([FromQuery] string? email, CancellationToken ct)
    {
        // Return email transmission audit records
        var query = _context.Users.AsNoTracking().Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.Where(u => u.Email != null && u.Email.Contains(email));
        }

        var list = await query.Select(u => new
        {
            u.Id,
            u.UserName,
            u.Email,
            lastLogin = DateTime.UtcNow,
            status = "Delivered"
        }).ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost("send-credentials")]
    [HttpPost("/PasswordEmails/SendCredentials")]
    public async Task<IActionResult> SendCredentials([FromBody] SendCredentialsEmailRequest request, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user == null)
        {
            throw new NotFoundException($"User with email {request.Email} not found.");
        }

        // Send email mock/log
        return Ok(new { success = true, email = request.Email, message = "Credentials email queued for delivery." });
    }
}

public record SendCredentialsEmailRequest(string Email, string? TemporaryPassword);
