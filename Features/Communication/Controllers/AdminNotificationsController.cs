using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/communication/notifications")]
[Route("AdminNotifications")]
public class AdminNotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminNotificationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetNotifications")]
    public async Task<ActionResult<List<AdminNotification>>> GetNotifications(CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        var list = await _context.Set<AdminNotification>()
            .AsNoTracking()
            .Where(n => n.TargetUserId == null || n.TargetUserId == currentUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return Ok(list);
    }
}
