using Luxira.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/delivery-companies/cities-without-prices")]
[Route("CitiesWithoutDeliveryPrices")]
public class CitiesWithoutDeliveryPricesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CitiesWithoutDeliveryPricesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetCities")]
    public async Task<ActionResult<List<string>>> GetCitiesWithoutPrices([FromQuery] int deliveryCompanyId, CancellationToken ct)
    {
        var pricedStates = await _context.DeliveryCompanyPrices
            .Where(p => p.DeliveryCompanyId == deliveryCompanyId)
            .Select(p => p.Country.ToString())
            .ToListAsync(ct);

        // List of major states
        var allStates = new List<string> { "بغداد", "البصرة", "أربيل", "النجف", "كربلاء", "الموصل", "كركوك", "بابل", "الأنبار", "ديالى", "ميسان", "ذي قار", "المثنى", "القادسية", "واسط", "صلاح الدين", "دهوك", "السليمانية" };
        var missing = allStates.Where(s => !pricedStates.Contains(s)).ToList();

        return Ok(missing);
    }
}
