using Luxira.Api.Features.Communication.DTOs;
using Luxira.Api.Features.Communication.Services;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector,Administrator")]
[Route("api/v1/communication/password-emails")]
[Route("PasswordEmails")]
public sealed class PasswordEmailsController(PasswordEmailService service) : ControllerBase
{
    [HttpGet]
    [HttpGet("/PasswordPages/PasswordEmails_Index")]
    public async Task<ActionResult<List<PasswordEmailDto>>> List(
        [FromQuery] string? emailFilter,
        [FromQuery] int? storeId,
        CancellationToken ct) =>
        Ok(await service.ListAsync(false, emailFilter, storeId, ct));

    [HttpGet("{id:int}")]
    [HttpGet("/PasswordEmails/Get")]
    public async Task<ActionResult<PasswordEmailDto>> Get(
        [FromRoute] int id,
        CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    [HttpPost("/PasswordPages/PasswordEmails_Create")]
    public async Task<ActionResult<PasswordEmailDto>> Create(
        [FromBody] SavePasswordEmailRequest request,
        CancellationToken ct)
    {
        var created = await service.CreateAsync(request, GetActor(), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [HttpPost("/PasswordEmails/Update")]
    public async Task<ActionResult<PasswordEmailDto>> Update(
        [FromRoute] int id,
        [FromBody] SavePasswordEmailRequest request,
        CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, request, GetActor(), ct));

    [HttpDelete("{id:int}")]
    [HttpPost("/PasswordEmails/Delete")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, GetActor(), ct);
        return Ok(new { success = true });
    }

    [HttpGet("trash")]
    [HttpGet("/PasswordEmails/TrashItems")]
    public async Task<ActionResult<List<PasswordEmailDto>>> Trash(CancellationToken ct) =>
        Ok(await service.ListAsync(true, null, null, ct));

    [HttpPost("{id:int}/restore")]
    [HttpPost("/PasswordEmails/Restore")]
    public async Task<IActionResult> Restore([FromRoute] int id, CancellationToken ct)
    {
        await service.RestoreAsync(id, GetActor(), ct);
        return Ok(new { success = true });
    }

    [Authorize(Roles = "Admin,Administrator")]
    [HttpDelete("{id:int}/permanent")]
    [HttpPost("/PasswordEmails/PermanentDelete")]
    public async Task<IActionResult> PermanentlyDelete([FromRoute] int id, CancellationToken ct)
    {
        await service.PermanentlyDeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("history")]
    [HttpGet("/PasswordEmails/EditHistory")]
    public async Task<ActionResult<List<PasswordEmailHistoryDto>>> History(
        [FromQuery] int? id,
        CancellationToken ct) =>
        Ok(await service.ListHistoryAsync(id, ct));

    private PasswordEmailActor GetActor() => new(
        User.GetUserId() ?? "system",
        User.Identity?.Name);
}
