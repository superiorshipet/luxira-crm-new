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
[Authorize]
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
    [HttpGet("Index")]
    [HttpGet("GetMainWarehouses")]
    public async Task<ActionResult<List<MainWarehouseDto>>> GetMainWarehouses([FromQuery] int? countryId, CancellationToken ct)
    {
        var result = await _service.GetMainWarehousesAsync(countryId, ct);
        return Ok(result);
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
