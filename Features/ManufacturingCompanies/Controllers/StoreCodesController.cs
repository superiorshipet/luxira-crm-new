using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/manufacturing-companies/store-codes")]
[Route("StoreCodes")]
public class StoreCodesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StoreCodesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("/StoreCodes/GetStoreCodes")]
    public async Task<ActionResult<List<StoreCodeFolder>>> GetStoreCodes([FromQuery] int? manufacturingCompanyId, CancellationToken ct)
    {
        var query = _context.StoreCodeFolders.AsNoTracking().AsQueryable();
        if (manufacturingCompanyId.HasValue && manufacturingCompanyId.Value > 0)
        {
            query = query.Where(s => s.ManufacturingCompanyId == manufacturingCompanyId.Value);
        }

        var list = await query.ToListAsync(ct);
        return Ok(list);
    }
}
