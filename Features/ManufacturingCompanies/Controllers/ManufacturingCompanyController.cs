using Luxira.Api.Features.ManufacturingCompanies.DTOs;
using Luxira.Api.Features.ManufacturingCompanies.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/manufacturing-companies")]
[Route("api/[controller]")]
public class ManufacturingCompanyController : ControllerBase
{
    private readonly ManufacturingCompanyService _service;

    public ManufacturingCompanyController(ManufacturingCompanyService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("/ManufacturingCompany/GetCompanies")]
    public async Task<ActionResult<List<ManufacturingCompanyDto>>> GetCompanies([FromQuery] int? countryId, [FromQuery] bool? isActive, CancellationToken ct)
    {
        var result = await _service.GetCompaniesAsync(countryId, isActive, ct);
        return Ok(result);
    }

    [HttpPost]
    [HttpPost("/ManufacturingCompany/Create")]
    public async Task<ActionResult<ManufacturingCompanyDto>> CreateCompany([FromBody] CreateManufacturingCompanyRequest request, CancellationToken ct)
    {
        var result = await _service.CreateCompanyAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("products")]
    [HttpGet("/MainProduct/GetProducts")]
    public async Task<ActionResult<List<ProductDto>>> GetProducts([FromQuery] int? companyId, CancellationToken ct)
    {
        var result = await _service.GetProductsAsync(companyId, ct);
        return Ok(result);
    }

    [HttpPost("products")]
    [HttpPost("/MainProduct/Create")]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var result = await _service.CreateProductAsync(request, ct);
        return Ok(result);
    }
}
