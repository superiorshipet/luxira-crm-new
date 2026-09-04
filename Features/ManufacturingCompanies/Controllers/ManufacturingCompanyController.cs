using Luxira.Api.Features.ManufacturingCompanies.DTOs;
using Luxira.Api.Features.ManufacturingCompanies.Services;
using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Features.Warehouses.Models;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,Accountant,ExecutiveDirector,Observer,OrderPreparer,DeliveryCompany,DeliveryRepresentative")]
[Route("api/v1/manufacturing-companies")]
[Route("api/[controller]")]
public class ManufacturingCompanyController : ControllerBase
{
    private readonly ManufacturingCompanyService _service;
    private readonly ApplicationDbContext _context;
    private readonly S3StorageService _storage;

    public ManufacturingCompanyController(ManufacturingCompanyService service, ApplicationDbContext context, S3StorageService storage)
    {
        _service = service;
        _context = context;
        _storage = storage;
    }

    [HttpGet]
    [HttpGet("/ManufacturingCompany/Index")]
    [HttpPost("/ManufacturingCompany/Index")]
    [HttpGet("/ManufacturingCompany/GetCompanies")]
    public async Task<ActionResult<List<ManufacturingCompanyDto>>> GetCompanies([FromQuery] int? countryId, [FromQuery] bool? isActive, CancellationToken ct)
    {
        var result = await _service.GetCompaniesAsync(countryId, isActive, ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<ActionResult<ManufacturingCompanyDto>> CreateCompany([FromBody] CreateManufacturingCompanyRequest request, CancellationToken ct)
    {
        var result = await _service.CreateCompanyAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("/ManufacturingCompany/Create")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> Create(CancellationToken ct) => Ok(new
    {
        mainWarehouses = await _context.MainWarehouses.AsNoTracking().OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(ct),
        selectedMainWarehouseIds = Array.Empty<int>()
    });

    [HttpPost("/ManufacturingCompany/Create")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> Create(
        [FromForm] string name,
        [FromForm] string? phoneNumber,
        [FromForm] List<int>? mainWarehouseIds,
        [FromForm] int? mainWarehouseId,
        [FromForm] IFormFile? logoFile,
        [FromForm] IFormFile? logoFile2,
        [FromForm] IFormFile? invoiceFile,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return Ok(new { success = false, message = "البيانات المدخلة غير صالحة" });
        var selected = NormalizeWarehouseIds(mainWarehouseIds, mainWarehouseId);
        var company = new ManufacturingCompany { Name = name.Trim(), PhoneNumber = phoneNumber?.Trim(), MainWarehouseId = selected.FirstOrDefault() is var first && first > 0 ? first : null, IsShown = true };
        await UpdateImagesAsync(company, logoFile, logoFile2, invoiceFile, ct);
        _context.ManufacturingCompanies.Add(company);
        await _context.SaveChangesAsync(ct);
        _context.ManufacturingCompanyMainWarehouses.AddRange(selected.Select(id => new ManufacturingCompanyMainWarehouse { ManufacturingCompanyId = company.Id, MainWarehouseId = id }));
        var employees = await _context.Employees.AsNoTracking().Select(employee => new { employee.Id, employee.ApplicationUserId }).ToListAsync(ct);
        _context.EmployeeManufacturingCompanies.AddRange(employees.Select(employee => new EmployeeManufacturingCompany { EmployeeId = employee.Id, ManufacturingCompanyId = company.Id, ApplicationUserId = employee.ApplicationUserId ?? string.Empty, CanSeeManufacturingCompany = false }));
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, redirectUrl = "/ManufacturingCompany/Index", id = company.Id });
    }

    [HttpGet("/ManufacturingCompany/Edit")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> Edit([FromQuery] int id, CancellationToken ct)
    {
        var company = await _context.ManufacturingCompanies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (company is null) return NotFound();
        var selected = await _context.ManufacturingCompanyMainWarehouses.AsNoTracking().Where(item => item.ManufacturingCompanyId == id).Select(item => item.MainWarehouseId).ToListAsync(ct);
        if (selected.Count == 0 && company.MainWarehouseId.HasValue) selected.Add(company.MainWarehouseId.Value);
        return Ok(new { company, selectedMainWarehouseIds = selected });
    }

    [HttpPost("/ManufacturingCompany/Edit")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> Edit(
        [FromQuery] int id,
        [FromForm] string name,
        [FromForm] string? phoneNumber,
        [FromForm] bool? isShown,
        [FromForm] List<int>? mainWarehouseIds,
        [FromForm] int? mainWarehouseId,
        [FromForm] IFormFile? logoFile,
        [FromForm] IFormFile? logoFile2,
        [FromForm] IFormFile? invoiceFile,
        CancellationToken ct)
    {
        var company = await _context.ManufacturingCompanies.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (company is null) return NotFound();
        var selected = NormalizeWarehouseIds(mainWarehouseIds, mainWarehouseId);
        company.Name = name.Trim();
        company.PhoneNumber = phoneNumber?.Trim();
        company.MainWarehouseId = selected.Count > 0 ? selected[0] : null;
        if (isShown.HasValue) company.IsShown = isShown.Value;
        await UpdateImagesAsync(company, logoFile, logoFile2, invoiceFile, ct);
        var old = await _context.ManufacturingCompanyMainWarehouses.Where(item => item.ManufacturingCompanyId == id).ToListAsync(ct);
        _context.ManufacturingCompanyMainWarehouses.RemoveRange(old);
        _context.ManufacturingCompanyMainWarehouses.AddRange(selected.Select(warehouseId => new ManufacturingCompanyMainWarehouse { ManufacturingCompanyId = id, MainWarehouseId = warehouseId }));
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, redirectUrl = "/ManufacturingCompany/Index" });
    }

    [HttpPost("/ManufacturingCompany/SetIsShown")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> SetIsShown([FromForm] int manufacturingcompanyId, [FromForm] bool IsShown, CancellationToken ct)
    {
        var changed = await _context.ManufacturingCompanies.Where(item => item.Id == manufacturingcompanyId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsShown, IsShown), ct);
        return changed == 0 ? Ok(new { success = false, message = "Manufacturing company not found." }) : Ok(new { success = true });
    }

    [HttpGet("products")]
    [HttpGet("/MainProduct/GetProducts")]
    public async Task<ActionResult<List<ProductDto>>> GetProducts([FromQuery] int? companyId, CancellationToken ct)
    {
        var result = await _service.GetProductsAsync(companyId, ct);
        return Ok(result);
    }

    [HttpPost("products")]
    [HttpPost("/MainProduct/Create")]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var result = await _service.CreateProductAsync(request, ct);
        return Ok(result);
    }

    private static List<int> NormalizeWarehouseIds(List<int>? ids, int? fallback) => (ids ?? [])
        .Append(fallback ?? 0).Where(id => id > 0).Distinct().ToList();

    private async Task UpdateImagesAsync(ManufacturingCompany company, IFormFile? first, IFormFile? second, IFormFile? invoice, CancellationToken ct)
    {
        foreach (var entry in new[] { (File: first, Slot: 1), (File: second, Slot: 2), (File: invoice, Slot: 3) })
        {
            if (entry.File is null || entry.File.Length == 0) continue;
            var upload = await _storage.UploadAsync(entry.File, "Stores", User.GetUserId(), ct);
            var url = $"/Media/File?key={Uri.EscapeDataString(upload.S3Key)}";
            if (entry.Slot == 1) { company.ImageUrl = url; company.ImageS3Key = upload.S3Key; }
            else if (entry.Slot == 2) { company.ImageUrl2 = url; company.ImageUrl2S3Key = upload.S3Key; }
            else { company.InvoiceImage = url; company.InvoiceImageS3Key = upload.S3Key; }
        }
    }
}
