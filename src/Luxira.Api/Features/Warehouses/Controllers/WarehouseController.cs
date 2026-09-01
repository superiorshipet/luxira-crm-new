using Luxira.Api.Features.Warehouses.DTOs;
using Luxira.Api.Features.Warehouses.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Warehouses.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/warehouses")]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly WarehouseService _service;

    public WarehouseController(WarehouseService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("/Warehouse/GetWarehouses")]
    public async Task<ActionResult<List<WarehouseDto>>> GetWarehouses([FromQuery] int? countryId, [FromQuery] bool? isActive, CancellationToken ct)
    {
        var result = await _service.GetWarehousesAsync(countryId, isActive, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HttpGet("/Warehouse/GetWarehouseById/{id:int}")]
    public async Task<ActionResult<WarehouseDto>> GetWarehouseById(int id, CancellationToken ct)
    {
        var result = await _service.GetWarehouseByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [HttpPost("/Warehouse/Create")]
    public async Task<ActionResult<WarehouseDto>> CreateWarehouse([FromBody] CreateWarehouseRequest request, CancellationToken ct)
    {
        var result = await _service.CreateWarehouseAsync(request, ct);
        return CreatedAtAction(nameof(GetWarehouseById), new { id = result.Id }, result);
    }

    [HttpGet("main")]
    [HttpGet("/MainWarehouse/GetMainWarehouses")]
    public async Task<ActionResult<List<MainWarehouseDto>>> GetMainWarehouses([FromQuery] int? countryId, CancellationToken ct)
    {
        var result = await _service.GetMainWarehousesAsync(countryId, ct);
        return Ok(result);
    }
}
