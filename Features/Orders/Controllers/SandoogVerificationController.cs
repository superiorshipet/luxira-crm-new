using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,CallCenter,FollowUpDepartment,ExecutiveDirector")]
[Route("SandoogVerification")]
public sealed class SandoogVerificationController : ControllerBase
{
    [HttpGet("Index")]
    public IActionResult Index() => RedirectPermanent("/PendingVerification/Company?id=sandoog");

    [HttpPost("Confirm")]
    public IActionResult Confirm() => Ok(new { success = false, message = "تم نقل شاشة قيد التحقق. يرجى تحديث الصفحة ثم إعادة المحاولة." });
}
