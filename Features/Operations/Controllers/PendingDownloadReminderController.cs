using Luxira.Api.Data;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/operations/pending-downloads")]
[Route("PendingDownloadReminder")]
public class PendingDownloadReminderController : ControllerBase
{
    [HttpPost("Check")]
    [HttpPost("/PendingDownloadReminder/Check")]
    public IActionResult Check()
    {
        return Ok(new { hasPendingDownloads = false, message = "Queue clear." });
    }

    [HttpGet("Unread")]
    [HttpGet("/PendingDownloadReminder/Unread")]
    public IActionResult Unread()
    {
        return Ok(new List<object>());
    }

    [HttpPost("MarkRead")]
    [HttpPost("/PendingDownloadReminder/MarkRead")]
    public IActionResult MarkRead([FromQuery] long id)
    {
        return Ok(new { success = true, id });
    }
}

[ApiController]
[AllowAnonymous]
[Route("api/client-timing")]
public class ClientTimingController : ControllerBase
{
    [HttpPost("home-table")]
    public IActionResult HomeTable([FromBody] object payload)
    {
        // Telemetry / timing ingestion endpoint
        return Ok(new { recorded = true, timestamp = IstanbulTimeHelper.Now });
    }
}
