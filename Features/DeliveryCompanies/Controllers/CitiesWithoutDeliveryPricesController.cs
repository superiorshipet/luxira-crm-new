using System.Globalization;
using System.Text;
using Luxira.Api.Data;
using Luxira.Api.Features.DeliveryCompanies.Models;
using Luxira.Api.Features.ReferenceData.Countries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector")]
[Route("api/v1/delivery-companies/cities-without-prices")]
[Route("CitiesWithoutDeliveryPrices")]
public sealed class CitiesWithoutDeliveryPricesController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    [HttpGet("GetCities")]
    public async Task<ActionResult<List<string>>> GetCitiesWithoutPrices([FromQuery] int deliveryCompanyId, CancellationToken ct)
    {
        var company = await context.DeliveryCompanies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == deliveryCompanyId, ct);
        if (company is null) return NotFound();
        var priced = await context.DeliveryCompanyPrices.AsNoTracking()
            .Where(price => price.DeliveryCompanyId == deliveryCompanyId && price.Country == company.Country && price.City != null)
            .Select(price => price.City!).ToListAsync(ct);
        var keys = priced.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Ok(CountryCityCatalog.GetDistinctCities([company.Country]).Where(city => !keys.Contains(Normalize(city))).ToList());
    }

    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] int? country, [FromQuery] int? deliveryCompanyId, [FromQuery] string? city, CancellationToken ct)
    {
        var companiesQuery = context.DeliveryCompanies.AsNoTracking().Where(company => company.IsShown && !company.IsRepresentative);
        if (country.HasValue) companiesQuery = companiesQuery.Where(company => company.Country == country.Value);
        if (deliveryCompanyId.HasValue) companiesQuery = companiesQuery.Where(company => company.Id == deliveryCompanyId.Value);
        var companies = await companiesQuery.OrderBy(company => company.Country).ThenBy(company => company.Name).ToListAsync(ct);
        var companyIds = companies.Select(company => company.Id).ToList();
        var prices = await context.DeliveryCompanyPrices.AsNoTracking().Where(price => price.DeliveryCompanyId.HasValue && companyIds.Contains(price.DeliveryCompanyId.Value) && price.City != null)
            .Select(price => new { price.DeliveryCompanyId, price.Country, price.City }).ToListAsync(ct);
        var priceKeys = prices.Select(price => Key(price.DeliveryCompanyId!.Value, price.Country, price.City!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = companies.SelectMany(company => CountryCityCatalog.GetDistinctCities([company.Country])
                .Where(cityName => !priceKeys.Contains(Key(company.Id, company.Country, cityName)))
                .Select(cityName => new { DeliveryCompanyId = company.Id, DeliveryCompanyName = company.Name, Country = company.Country, CityName = cityName }))
            .Where(row => string.IsNullOrWhiteSpace(city) || Normalize(row.CityName) == Normalize(city))
            .OrderBy(row => row.Country).ThenBy(row => row.DeliveryCompanyName).ThenBy(row => row.CityName).ToList();
        return Ok(new { rows, total = rows.Count });
    }

    [HttpPost("CopyKamexPricesToLibyaMissingCities")]
    public async Task<IActionResult> CopyKamexPricesToLibyaMissingCities(CancellationToken ct)
    {
        const int libya = 4;
        var companies = await context.DeliveryCompanies.AsNoTracking()
            .Where(company => company.IsShown && !company.IsRepresentative && company.Country == libya)
            .Select(company => new { company.Id, company.Name, company.DisplayName }).ToListAsync(ct);
        var camexIds = companies.Where(company => IsCamex(company.Name) || IsCamex(company.DisplayName)).Select(company => company.Id).ToHashSet();
        if (camexIds.Count == 0) return NotFound(new { success = false, message = "لم يتم العثور على شركة كامكس مفعلة داخل ليبيا." });
        var sourceRows = await context.DeliveryCompanyPrices.AsNoTracking().Where(price => price.DeliveryCompanyId.HasValue && camexIds.Contains(price.DeliveryCompanyId.Value) && price.Country == libya && price.City != null)
            .OrderByDescending(price => price.Id).ToListAsync(ct);
        var source = sourceRows.GroupBy(price => Normalize(price.City!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Price, StringComparer.OrdinalIgnoreCase);
        if (source.Count == 0) return Conflict(new { success = false, message = "شركة كامكس لا يوجد لها أي أسعار مدن مسجلة داخل ليبيا." });
        var targets = companies.Where(company => !camexIds.Contains(company.Id)).ToList();
        var targetIds = targets.Select(company => company.Id).ToList();
        var existingRows = await context.DeliveryCompanyPrices.AsNoTracking().Where(price => price.DeliveryCompanyId.HasValue && targetIds.Contains(price.DeliveryCompanyId.Value) && price.Country == libya && price.City != null)
            .Select(price => new { price.DeliveryCompanyId, price.City }).ToListAsync(ct);
        var existing = existingRows.Select(price => Key(price.DeliveryCompanyId!.Value, libya, price.City!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var additions = new List<DeliveryCompanyPrice>();
        var skipped = 0;
        foreach (var target in targets)
        foreach (var cityName in CountryCityCatalog.GetDistinctCities([libya]))
        {
            var key = Key(target.Id, libya, cityName);
            if (existing.Contains(key)) continue;
            if (!source.TryGetValue(Normalize(cityName), out var price)) { skipped++; continue; }
            additions.Add(new DeliveryCompanyPrice { DeliveryCompanyId = target.Id, Country = libya, City = cityName, Price = price });
            existing.Add(key);
        }
        if (additions.Count > 0) { await context.DeliveryCompanyPrices.AddRangeAsync(additions, ct); await context.SaveChangesAsync(ct); }
        return Ok(new { success = true, addedCount = additions.Count, skippedCount = skipped });
    }

    private static string Key(int companyId, int country, string city) => $"{companyId}:{country}:{Normalize(city)}";
    private static bool IsCamex(string? value) { var normalized = Normalize(value ?? string.Empty); return normalized.Contains("camex", StringComparison.OrdinalIgnoreCase) || normalized.Contains("kamex", StringComparison.OrdinalIgnoreCase) || normalized.Contains("كامكس", StringComparison.Ordinal); }
    private static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark || char.IsWhiteSpace(character)) continue;
            builder.Append(character switch { 'أ' or 'إ' or 'آ' => 'ا', 'ى' => 'ي', 'ة' => 'ه', _ => char.ToLowerInvariant(character) });
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
