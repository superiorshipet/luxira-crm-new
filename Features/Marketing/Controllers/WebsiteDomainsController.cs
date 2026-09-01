using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/marketing/domains")]
[Route("WebsiteDomains")]
public class WebsiteDomainsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public WebsiteDomainsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetDomains")]
    public async Task<ActionResult<List<WebsiteDomain>>> GetDomains(CancellationToken ct)
    {
        var list = await _context.Set<WebsiteDomain>().AsNoTracking().ToListAsync(ct);
        return Ok(list);
    }
}
