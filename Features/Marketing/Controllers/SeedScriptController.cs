using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
public sealed class SeedScriptController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet("/seedscript/test-payload.js")]
    [AllowAnonymous]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> TestPayload(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        var script = await context.SeedScriptSettings.AsNoTracking().OrderBy(item => item.Id).Select(item => item.Message).FirstOrDefaultAsync(ct) ?? "alert('hello world');";
        return Content(script, "application/javascript");
    }

    [HttpPost("/admindashboard/seed-script-test-message")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> UpdateTestMessage([FromForm] string message, CancellationToken ct)
    {
        message = message?.Trim() ?? string.Empty;
        if (message.Length == 0) return BadRequest(new { message = "message is required" });
        var setting = await context.SeedScriptSettings.OrderBy(item => item.Id).FirstOrDefaultAsync(ct);
        if (setting is null) { setting = new SeedScriptSetting(); context.SeedScriptSettings.Add(setting); }
        setting.Message = message; setting.UpdatedAt = DateTime.Now; setting.UpdatedBy = User.Identity?.Name;
        await context.SaveChangesAsync(ct);
        return Ok(new { setting.Message, setting.UpdatedAt, setting.UpdatedBy });
    }
}
