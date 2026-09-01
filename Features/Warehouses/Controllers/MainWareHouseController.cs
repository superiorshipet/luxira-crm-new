using Luxira.Api.Features.Warehouses.DTOs;
using Luxira.Api.Features.Warehouses.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Warehouses.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/warehouses/main")]
[Route("MainWareHouse")]
public class MainWareHouseController : ControllerBase
{
    private readonly WarehouseService _service;

    public MainWareHouseController(WarehouseService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("GetMainWarehouses")]
    public async Task<ActionResult<List<MainWarehouseDto>>> GetMainWarehouses([FromQuery] int? countryId, CancellationToken ct)
    {
        var result = await _service.GetMainWarehousesAsync(countryId, ct);
        return Ok(result);
    }
}
