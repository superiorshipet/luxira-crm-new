using Luxira.Api.Data;
using Luxira.Api.Features.Communication.DTOs;
using Luxira.Api.Features.Communication.Services;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector")]
[Route("api/v1/communication/password-emails")]
public sealed class PasswordEmailsController(PasswordEmailService service, ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PasswordEmailDto>>> List(string? emailFilter, int? storeId, CancellationToken ct) =>
        Ok(await service.ListAsync(false, emailFilter, storeId, ct));

    [HttpGet("/PasswordPages/PasswordEmails_Index")]
    [HttpGet("/PasswordEmails")]
    public async Task<IActionResult> LegacyIndex(string? emailFilter, int? storeId, CancellationToken ct)
    {
        var items = await service.ListAsync(false, emailFilter, storeId, ct);
        var total = await service.ListAsync(false, null, null, ct);
        return Ok(new
        {
            success = true,
            emails = items,
            emailOptions = total.Select(item => item.Email).Distinct().OrderBy(email => email),
            stores = await StoreOptions(ct),
            emailFilter,
            storeFilterId = storeId,
            filteredCount = items.Count,
            totalCount = total.Count
        });
    }

    [HttpGet("/PasswordPages/PasswordEmails_Create")]
    [HttpGet("/PasswordEmails/Create")]
    public async Task<IActionResult> CreateForm(CancellationToken ct) => Ok(new { success = true, stores = await StoreOptions(ct) });

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PasswordEmailDto>> Get(int id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));

    [HttpGet("/PasswordEmails/Get")]
    public async Task<IActionResult> GetLegacy([FromQuery] int id, CancellationToken ct) =>
        Ok(new { success = true, item = await service.GetAsync(id, ct) });

    [HttpPost]
    public async Task<ActionResult<PasswordEmailDto>> Create([FromBody] SavePasswordEmailRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, GetActor(), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("/PasswordPages/PasswordEmails_Create")]
    [HttpPost("/PasswordEmails/Create")]
    public async Task<IActionResult> CreateLegacy([FromForm] SavePasswordEmailRequest request, CancellationToken ct)
    {
        var item = await service.CreateAsync(request, GetActor(), ct);
        return Ok(new { success = true, message = "تمت إضافة البريد الإلكتروني بنجاح", item });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PasswordEmailDto>> Update(int id, [FromBody] SavePasswordEmailRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, GetActor(), ct));

    [HttpPost("/PasswordEmails/Update")]
    public async Task<IActionResult> UpdateLegacy([FromForm] PasswordEmailLegacyUpdate request, CancellationToken ct)
    {
        var item = await service.UpdateAsync(request.Id, request, GetActor(), ct);
        return Ok(new { success = true, message = "تم تعديل البريد الإلكتروني بنجاح", item });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, GetActor(), ct);
        return Ok(new { success = true });
    }

    [HttpPost("/PasswordEmails/Delete")]
    public async Task<IActionResult> DeleteLegacy([FromForm] int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, GetActor(), ct);
        return Ok(new { success = true, message = "تم نقل البريد الإلكتروني إلى سلة المهملات" });
    }

    [HttpGet("trash")]
    [HttpGet("/PasswordEmails/TrashItems")]
    public async Task<IActionResult> Trash(CancellationToken ct)
    {
        var items = await service.ListAsync(true, null, null, ct);
        return Ok(new { success = true, items });
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id, CancellationToken ct)
    {
        await service.RestoreAsync(id, GetActor(), ct);
        return Ok(new { success = true });
    }

    [HttpPost("/PasswordEmails/Restore")]
    public async Task<IActionResult> RestoreLegacy([FromForm] int id, CancellationToken ct)
    {
        await service.RestoreAsync(id, GetActor(), ct);
        return Ok(new { success = true, message = "تم استرداد البريد الإلكتروني" });
    }

    [Authorize(Roles = "Admin,ExecutiveDirector")]
    [HttpDelete("{id:int}/permanent")]
    public async Task<IActionResult> PermanentlyDelete(int id, CancellationToken ct)
    {
        await service.PermanentlyDeleteAsync(id, ct);
        return NoContent();
    }

    [Authorize(Roles = "Admin,ExecutiveDirector")]
    [HttpPost("/PasswordEmails/PermanentDelete")]
    public async Task<IActionResult> PermanentlyDeleteLegacy([FromForm] int id, CancellationToken ct)
    {
        await service.PermanentlyDeleteAsync(id, ct);
        return Ok(new { success = true, message = "تم الحذف النهائي" });
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<PasswordEmailHistoryDto>>> History(int? id, CancellationToken ct) =>
        Ok(await service.ListHistoryAsync(id, ct));

    [HttpGet("/PasswordEmails/EditHistory")]
    public async Task<IActionResult> LegacyHistory(CancellationToken ct) =>
        Ok(new { success = true, items = await service.ListHistoryAsync(null, ct) });

    private async Task<object[]> StoreOptions(CancellationToken ct) => (await context.ManufacturingCompanies.AsNoTracking()
            .OrderBy(store => store.Name).Select(store => new { store.Id, store.Name, store.ImageUrl }).ToListAsync(ct))
        .Cast<object>().ToArray();

    private PasswordEmailActor GetActor() => new(User.GetUserId() ?? "system", User.Identity?.Name);
}

public sealed record PasswordEmailLegacyUpdate(
    int Id,
    int ManufacturingCompanyId,
    string Email,
    string Password,
    string? PhoneNumber,
    string? PageStatusName)
    : SavePasswordEmailRequest(ManufacturingCompanyId, Email, Password, PhoneNumber, PageStatusName);
