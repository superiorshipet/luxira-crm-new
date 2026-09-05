using Luxira.Api.Features.Warehouses.DTOs;
using Luxira.Api.Features.Warehouses.Services;
using Luxira.Api.Data;
using Luxira.Api.Features.Warehouses.Models;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Warehouses.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
[Route("api/v1/warehouses/main")]
[Route("MainWareHouse")]
public class MainWareHouseController : ControllerBase
{
    private readonly WarehouseService _service;
    private readonly ApplicationDbContext _context;
    private readonly S3StorageService _storage;

    public MainWareHouseController(WarehouseService service, ApplicationDbContext context, S3StorageService storage)
    {
        _service = service;
        _context = context;
        _storage = storage;
    }

    [HttpGet]
    [HttpGet("GetMainWarehouses")]
    public async Task<ActionResult<List<MainWarehouseDto>>> GetMainWarehouses([FromQuery] int? countryId, CancellationToken ct)
    {
        var result = await _service.GetMainWarehousesAsync(countryId, ct);
        return Ok(result);
    }

    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        [FromQuery] int page = 1,
        [FromQuery] int? pageSize = null,
        [FromQuery] int? mainwarehouseId = null,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        var effectivePageSize = Math.Clamp(pageSize ?? 10, 1, 200);
        var query = _context.MainWarehouses.AsNoTracking();
        if (mainwarehouseId.HasValue) query = query.Where(item => item.Id == mainwarehouseId.Value);

        var totalItems = await query.CountAsync(ct);
        var items = await query.OrderByDescending(item => item.Id)
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(item => new { item.Id, item.Name, item.ImageUrl })
            .ToListAsync(ct);
        return Ok(new { items, currentPage = page, pageSize = effectivePageSize, totalItems });
    }

    [HttpGet("Create")]
    public IActionResult Create() => Ok(new { name = string.Empty, imageUrl = (string?)null });

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromForm] string name, [FromForm] IFormFile? prodtuctimage, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "حقل الاسم مطلوب." });
        string? imageUrl = null;
        string? imageKey = null;
        if (prodtuctimage is not null && prodtuctimage.Length > 0)
        {
            var upload = await _storage.UploadAsync(prodtuctimage, "MainWarehouseImages", User.GetUserId(), ct);
            imageKey = upload.S3Key;
            imageUrl = $"/Media/File?key={Uri.EscapeDataString(upload.S3Key)}";
        }
        var item = new MainWarehouse { Name = name.Trim(), ImageUrl = imageUrl, ImageS3Key = imageKey };
        _context.MainWarehouses.Add(item);
        await _context.SaveChangesAsync(ct);
        return Ok(item);
    }

    [HttpGet("Edit")]
    public async Task<IActionResult> Edit([FromQuery] int id, CancellationToken ct)
    {
        var item = await _context.MainWarehouses.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("Edit")]
    public async Task<IActionResult> Edit([FromQuery] int id, [FromForm] string name, [FromForm] IFormFile? productImage, CancellationToken ct)
    {
        var item = await _context.MainWarehouses.FirstOrDefaultAsync(row => row.Id == id, ct);
        if (item is null) return NotFound();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "حقل الاسم مطلوب." });
        if (productImage is not null && productImage.Length > 0)
        {
            var oldKey = item.ImageS3Key;
            var upload = await _storage.UploadAsync(productImage, "MainWarehouseImages", User.GetUserId(), ct);
            item.ImageS3Key = upload.S3Key;
            item.ImageUrl = $"/Media/File?key={Uri.EscapeDataString(upload.S3Key)}";
            if (!string.IsNullOrWhiteSpace(oldKey))
            {
                try { await _storage.DeleteAsync(oldKey, ct); } catch { }
            }
        }
        item.Name = name.Trim();
        await _context.SaveChangesAsync(ct);
        return Ok(item);
    }
}
