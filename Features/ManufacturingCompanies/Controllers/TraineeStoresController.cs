using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
[Route("api/v1/manufacturing/trainee-stores")]
[Route("TraineeStores")]
public class TraineeStoresController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TraineeStoresController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/TraineeStores/Index")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var stores = await _context.StoreCodeFolders
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(stores);
    }

    [HttpPost("Create")]
    [HttpPost("/TraineeStores/Create")]
    public async Task<IActionResult> Create([FromBody] TraineeStoreSaveRequest request, CancellationToken ct = default)
    {
        var folder = new StoreCodeFolder
        {
            FolderName = request.StoreName,
            ManufacturingCompanyId = request.ManufacturingCompanyId
        };

        await _context.StoreCodeFolders.AddAsync(folder, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(folder);
    }

    [HttpPost("Update")]
    [HttpPost("/TraineeStores/Update")]
    public async Task<IActionResult> Update([FromBody] TraineeStoreSaveRequest request, CancellationToken ct = default)
    {
        var folder = await _context.StoreCodeFolders.FirstOrDefaultAsync(f => f.Id == request.Id, ct);
        if (folder == null) return NotFound("Store folder not found.");

        folder.FolderName = request.StoreName;
        folder.ManufacturingCompanyId = request.ManufacturingCompanyId;

        await _context.SaveChangesAsync(ct);
        return Ok(folder);
    }

    [HttpPost("Delete/{id:int}")]
    [HttpDelete("{id:int}")]
    [HttpPost("/TraineeStores/Delete")]
    public async Task<IActionResult> Delete([RouteOrRequest] int id, CancellationToken ct = default)
    {
        var folder = await _context.StoreCodeFolders.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (folder == null) return NotFound("Store folder not found.");

        _context.StoreCodeFolders.Remove(folder);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}

public record TraineeStoreSaveRequest(int? Id, string StoreName, int ManufacturingCompanyId);
