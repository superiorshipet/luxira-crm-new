using Luxira.Api.Features.ManufacturingCompanies.DTOs;
using Luxira.Api.Features.ManufacturingCompanies.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/product-prices")]
[Route("api/[controller]")]
public class ProductPriceController : ControllerBase
{
    private readonly ManufacturingCompanyService _service;

    public ProductPriceController(ManufacturingCompanyService service)
    {
        _service = service;
    }

    [HttpGet("minimum-prices")]
    [HttpGet("/ProductMinimumSellingPrices/GetPrices")]
    public async Task<ActionResult<List<ProductMinimumPriceDto>>> GetMinimumPrices(
        [FromQuery] int? productId,
        [FromQuery] int? countryId,
        CancellationToken ct)
    {
        var result = await _service.GetMinimumPricesAsync(productId, countryId, ct);
        return Ok(result);
    }

    [HttpPost("minimum-prices")]
    [HttpPost("/ProductMinimumSellingPrices/SetPrice")]
    public async Task<IActionResult> SetMinimumPrice([FromBody] SetProductMinimumPriceRequest request, CancellationToken ct)
    {
        await _service.SetMinimumPriceAsync(request, ct);
        return Ok(new { message = "Minimum product price set successfully." });
    }
}
