using Luxira.Api.Features.DeliveryCompanies.DTOs;
using Luxira.Api.Features.DeliveryCompanies.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[Route("api/v1/delivery-companies/{deliveryCompanyId:int}/price")]
[Route("api/[controller]")]
public class DeliveryPriceController : ControllerBase
{
    private readonly DeliveryCompanyService _service;

    public DeliveryPriceController(DeliveryCompanyService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("/DataList/GetDeliveryPrice")]
    public async Task<ActionResult<DeliveryPriceResult>> GetPrice(
        [FromRoute] int deliveryCompanyId,
        [FromQuery(Name = "countryId")] int countryId,
        [FromQuery(Name = "cityId")] string? cityId,
        CancellationToken ct)
    {
        var result = await _service.GetPriceAsync(deliveryCompanyId, countryId, cityId, ct);
        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> SetPrice([FromBody] SetDeliveryPriceRequest request, CancellationToken ct)
    {
        await _service.SetPriceAsync(request, ct);
        return Ok(new { msg = "Price updated successfully." });
    }
}
