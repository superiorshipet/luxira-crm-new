using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize]
[Route("Workspace")]
[Route("api/v1/operations/workspace")]
public sealed class WorkspaceController : ControllerBase
{
    [HttpGet]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        if (User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector"))
            return Redirect("/Home/Index");
        return Ok(new { workspace = true });
    }
}
