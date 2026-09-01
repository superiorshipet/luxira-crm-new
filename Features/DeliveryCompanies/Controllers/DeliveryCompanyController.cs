using System.Security.Claims;
using Luxira.Api.Features.DeliveryCompanies.DTOs;
using Luxira.Api.Features.DeliveryCompanies.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[Route("api/v1/delivery-companies")]
[Route("api/[controller]")]
public class DeliveryCompanyController : ControllerBase
{
    private readonly DeliveryCompanyService _service;

    public DeliveryCompanyController(DeliveryCompanyService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("/DataList/GetDeliveryCompanies")]
    public async Task<ActionResult<DeliveryCompanyResult>> GetCompanies([FromQuery] int? countryId, CancellationToken ct)
    {
        var result = await _service.ListCompaniesAsync(countryId, ct);
        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<DeliveryCompanyRecord>> CreateCompany([FromBody] CreateDeliveryCompanyRequest request, CancellationToken ct)
    {
        var userId = Luxira.Api.Utils.Extensions.ClaimsPrincipalExtensions.GetUserId(User) ?? "system";
        var result = await _service.CreateCompanyAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetCompanies), new { id = result.Id }, result);
    }
}
