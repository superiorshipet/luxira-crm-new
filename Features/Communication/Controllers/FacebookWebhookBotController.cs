using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/webhooks/facebook")]
[Route("FacebookWebhookBot")]
public class FacebookWebhookBotController : ControllerBase
{
    [HttpGet]
    [HttpGet("Webhook")]
    public IActionResult VerifyWebhook([FromQuery(Name = "hub.mode")] string? mode, [FromQuery(Name = "hub.verify_token")] string? token, [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe" && token == "luxira_facebook_verify_token")
        {
            return Ok(challenge);
        }

        return Forbid();
    }

    [HttpPost]
    [HttpPost("Webhook")]
    public IActionResult ReceiveWebhook([FromBody] object payload)
    {
        // Accept incoming Facebook Messenger/Comments events
        return Ok(new { status = "EVENT_RECEIVED" });
    }
}
