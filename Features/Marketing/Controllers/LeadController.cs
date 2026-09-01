using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
[Route("api/v1/marketing/leads")]
[Route("Lead")]
public class LeadController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public LeadController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetLeads")]
    public async Task<ActionResult<List<MarketingLead>>> GetLeads(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? orderSourceId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var query = _context.Set<MarketingLead>().AsNoTracking().AsQueryable();

        if (User.IsInRole("CallCenter") &&
            !User.IsInRole("Admin") &&
            !User.IsInRole("ExecutiveDirector") &&
            !User.IsInRole("FollowUpDepartment"))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(lead => lead.ApplicationUserId == currentUserId);
        }

        if (orderSourceId.HasValue)
        {
            query = query.Where(lead => lead.OrderSource == orderSourceId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(lead => lead.CreatedDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var exclusiveEnd = endDate.Value.Date.AddDays(1);
            query = query.Where(lead => lead.CreatedDate < exclusiveEnd);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(lead =>
                EF.Functions.Like(lead.SourceName, pattern) ||
                (lead.PhoneNumber != null && EF.Functions.Like(lead.PhoneNumber, pattern)) ||
                (lead.ChatUrl != null && EF.Functions.Like(lead.ChatUrl, pattern)));
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var list = await query
            .OrderByDescending(lead => lead.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return Ok(list);
    }
}
