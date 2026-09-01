using Luxira.Api.Features.DeliveryCompanies.DTOs;
using Luxira.Api.Features.DeliveryCompanies.Services;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[Route("api/v1/delivery-representatives")]
[Route("api/[controller]")]
public class DeliveryRepresentativeController : ControllerBase
{
    private readonly DeliveryCompanyService _service;

    public DeliveryRepresentativeController(DeliveryCompanyService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("/DataList/GetDeliveryRepresentatives")]
    public async Task<ActionResult<DeliveryRepresentativeResult>> GetRepresentatives([FromQuery] int? countryId, CancellationToken ct)
    {
        var result = await _service.ListRepresentativesAsync(countryId, ct);
        return Ok(result);
    }
}
