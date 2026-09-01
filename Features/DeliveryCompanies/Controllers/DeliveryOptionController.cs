using Luxira.Api.Features.DeliveryCompanies.DTOs;
using Luxira.Api.Features.DeliveryCompanies.Services;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[Route("api/v1/delivery-options")]
[Route("api/[controller]")]
public class DeliveryOptionController : ControllerBase
{
    private readonly DeliveryCompanyService _service;

    public DeliveryOptionController(DeliveryCompanyService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("/DataList/GetDeliveryOptions")]
    public async Task<ActionResult<DeliveryOptionResult>> GetOptions([FromQuery] int? countryId, CancellationToken ct)
    {
        var result = await _service.ListOptionsAsync(countryId, ct);
        return Ok(result);
    }
}
