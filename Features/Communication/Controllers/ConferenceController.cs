using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/communication/conference")]
[Route("Conference")]
public class ConferenceController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ConferenceController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetMeetings")]
    public async Task<ActionResult<List<ConferenceMeeting>>> GetMeetings(CancellationToken ct)
    {
        var list = await _context.Set<ConferenceMeeting>().AsNoTracking().ToListAsync(ct);
        return Ok(list);
    }
}
