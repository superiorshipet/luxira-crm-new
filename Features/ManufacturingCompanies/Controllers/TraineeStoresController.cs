using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Utils.Binding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector")]
[Route("api/v1/manufacturing/trainee-stores")]
public sealed class TraineeStoresController(
    ApplicationDbContext context,
    ILogger<TraineeStoresController> logger) : ControllerBase
{
    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/TraineeStores/Index")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var cards = await context.TraineeStores
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new TraineeStoreCardResponse(
                item.Id,
                item.Name,
                item.PhoneNumber ?? string.Empty,
                item.ManufacturingCompanies
                    .OrderBy(link => link.ManufacturingCompany!.Name)
                    .Select(link => new TraineeStoreItemResponse(
                        link.ManufacturingCompanyId,
                        link.ManufacturingCompany!.Name,
                        NormalizeImageUrl(link.ManufacturingCompany.ImageUrl)))
                    .ToList()))
            .ToListAsync(ct);

        var storeOptions = await context.ManufacturingCompanies
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new TraineeStoreItemResponse(item.Id, item.Name, NormalizeImageUrl(item.ImageUrl)))
            .ToListAsync(ct);

        return Ok(new
        {
            cards,
            storeOptions,
            phoneOptions = cards.Select(item => item.PhoneNumber)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList()
        });
    }

    [HttpPost("Create")]
    public Task<IActionResult> Create([FromBody] TraineeStoreSaveRequest request, CancellationToken ct = default) =>
        CreateCore(request, ct);

    [HttpPost("/TraineeStores/Create")]
    public Task<IActionResult> CreateLegacy([FromForm] TraineeStoreSaveRequest request, CancellationToken ct = default) =>
        CreateCore(request, ct);

    [HttpPost("Update")]
    public Task<IActionResult> Update([FromBody] TraineeStoreSaveRequest request, CancellationToken ct = default) =>
        UpdateCore(request, ct);

    [HttpPost("/TraineeStores/Update")]
    public Task<IActionResult> UpdateLegacy([FromForm] TraineeStoreSaveRequest request, CancellationToken ct = default) =>
        UpdateCore(request, ct);

    [HttpPost("Delete/{id:int}")]
    [HttpDelete("{id:int}")]
    [HttpPost("/TraineeStores/Delete")]
    public async Task<IActionResult> Delete([RouteOrRequest] int id, CancellationToken ct = default)
    {
        if (id <= 0) return Fail("بيانات غير صحيحة.");

        try
        {
            var trainee = await context.TraineeStores.FirstOrDefaultAsync(item => item.Id == id, ct);
            if (trainee is not null)
            {
                context.TraineeStores.Remove(trainee);
                await context.SaveChangesAsync(ct);
            }

            return Ok(new { success = true, id });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not delete trainee store {TraineeStoreId}", id);
            return Fail("تعذر حذف المتدرب.");
        }
    }

    private async Task<IActionResult> CreateCore(TraineeStoreSaveRequest request, CancellationToken ct)
    {
        var validation = await Validate(request, ct);
        if (validation.Error is not null) return Fail(validation.Error);

        var now = DateTime.UtcNow;
        var trainee = new TraineeStore
        {
            Name = request.Name.Trim(),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            CreatedAt = now,
            ManufacturingCompanies = validation.StoreIds
                .Select(storeId => new TraineeStoreManufacturingCompany
                {
                    ManufacturingCompanyId = storeId,
                    CreatedAt = now
                })
                .ToList()
        };

        try
        {
            context.TraineeStores.Add(trainee);
            await context.SaveChangesAsync(ct);
            return Ok(new { success = true, card = await GetCard(trainee.Id, ct) });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not create trainee store");
            return Fail("تعذر إضافة متجر المتدرب.");
        }
    }

    private async Task<IActionResult> UpdateCore(TraineeStoreSaveRequest request, CancellationToken ct)
    {
        if (request.Id <= 0) return Fail("بيانات غير صحيحة.");

        var trainee = await context.TraineeStores
            .Include(item => item.ManufacturingCompanies)
            .FirstOrDefaultAsync(item => item.Id == request.Id, ct);
        if (trainee is null) return Fail("المتدرب غير موجود.");

        var validation = await Validate(request, ct);
        if (validation.Error is not null) return Fail(validation.Error);

        trainee.Name = request.Name.Trim();
        trainee.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        trainee.UpdatedAt = DateTime.UtcNow;

        var requestedIds = validation.StoreIds.ToHashSet();
        var removed = trainee.ManufacturingCompanies.Where(link => !requestedIds.Contains(link.ManufacturingCompanyId)).ToList();
        context.TraineeStoreManufacturingCompanies.RemoveRange(removed);
        var existingIds = trainee.ManufacturingCompanies.Select(link => link.ManufacturingCompanyId).ToHashSet();
        foreach (var storeId in requestedIds.Where(storeId => !existingIds.Contains(storeId)))
        {
            trainee.ManufacturingCompanies.Add(new TraineeStoreManufacturingCompany
            {
                ManufacturingCompanyId = storeId,
                CreatedAt = trainee.UpdatedAt.Value
            });
        }

        try
        {
            await context.SaveChangesAsync(ct);
            return Ok(new { success = true, card = await GetCard(trainee.Id, ct) });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not update trainee store {TraineeStoreId}", trainee.Id);
            return Fail("تعذر تعديل بيانات المتدرب.");
        }
    }

    private async Task<(string? Error, List<int> StoreIds)> Validate(TraineeStoreSaveRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return ("اسم المتدرب مطلوب.", []);
        if (request.Name.Trim().Length > 200) return ("اسم المتدرب لا يزيد عن 200 حرف.", []);
        if ((request.PhoneNumber?.Trim().Length ?? 0) > 50) return ("رقم الهاتف لا يزيد عن 50 حرف.", []);

        var storeIds = request.StoreIds.Where(id => id > 0).Distinct().ToList();
        if (storeIds.Count == 0) return ("اختاري متجر واحد على الأقل.", []);

        var validIds = await context.ManufacturingCompanies.AsNoTracking()
            .Where(item => storeIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(ct);
        if (validIds.Count == 0) return ("المتاجر المختارة غير صحيحة.", []);

        return (null, validIds);
    }

    private async Task<TraineeStoreCardResponse> GetCard(int id, CancellationToken ct) =>
        await context.TraineeStores.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new TraineeStoreCardResponse(
                item.Id,
                item.Name,
                item.PhoneNumber ?? string.Empty,
                item.ManufacturingCompanies
                    .OrderBy(link => link.ManufacturingCompany!.Name)
                    .Select(link => new TraineeStoreItemResponse(
                        link.ManufacturingCompanyId,
                        link.ManufacturingCompany!.Name,
                        NormalizeImageUrl(link.ManufacturingCompany.ImageUrl)))
                    .ToList()))
            .SingleAsync(ct);

    private OkObjectResult Fail(string message) => Ok(new { success = false, message });
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "/static/DefaultImage.svg";
        var trimmed = value.Trim();
        return trimmed.StartsWith('/') || Uri.TryCreate(trimmed, UriKind.Absolute, out _) ? trimmed : $"/{trimmed}";
    }
}

public sealed class TraineeStoreSaveRequest
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public List<int> StoreIds { get; init; } = [];
}

public sealed record TraineeStoreCardResponse(int Id, string Name, string PhoneNumber, List<TraineeStoreItemResponse> Stores);
public sealed record TraineeStoreItemResponse(int Id, string Name, string ImageUrl);
