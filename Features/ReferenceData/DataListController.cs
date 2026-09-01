using Luxira.Api.Data;
using Luxira.Api.Features.ReferenceData.Countries;
using Luxira.Api.Features.ReferenceData.FailureReasons;
using Luxira.Api.Features.ReferenceData.OrderSources;
using Luxira.Api.Features.ReferenceData.OrderStatuses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.ReferenceData;

[ApiController]
[AllowAnonymous]
[Route("api/v1/reference-data/datalist")]
[Route("DataList")]
[Route("Api")]
public class DataListController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DataListController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("GetAllCountries")]
    public ActionResult<CountryContract[]> GetAllCountries() => Ok(CountryCatalog.All);

    [HttpGet("GetPfdCountries")]
    public ActionResult<CountryContract[]> GetPfdCountries() => Ok(CountryCatalog.PreparationForDelivery);

    [HttpGet("GetAllFailureReasons")]
    public ActionResult<FailureReasonContract[]> GetAllFailureReasons() => Ok(FailureReasonCatalog.All);

    [HttpGet("GetAllOrderSources")]
    public ActionResult<OrderSourceContract[]> GetAllOrderSources() => Ok(OrderSourceCatalog.All);

    [HttpGet("GetAllOrderStatuses")]
    public ActionResult<OrderStatusResponse[]> GetAllOrderStatuses() => Ok(OrderStatusCatalog.Administrators);

    [HttpGet("GetAllDeliveryCompanies")]
    public async Task<IActionResult> GetAllDeliveryCompanies([FromQuery] int? countryId, CancellationToken ct)
    {
        var query = _context.DeliveryCompanies.AsNoTracking().Where(d => d.IsActive);
        if (countryId.HasValue && countryId.Value > 0)
        {
            query = query.Where(d => d.Country == countryId.Value);
        }

        var list = await query.Select(d => new { id = d.Id, name = d.Name, logoUrl = d.LogoUrl ?? "/static/DefaultImage.svg" }).ToListAsync(ct);
        return Ok(list);
    }
}
