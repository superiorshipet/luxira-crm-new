using Luxira.Api.Data;
using Luxira.Api.Features.ReferenceData.Countries;
using Luxira.Api.Features.ReferenceData.FailureReasons;
using Luxira.Api.Features.ReferenceData.OrderSources;
using Luxira.Api.Features.ReferenceData.OrderStatuses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Luxira.Api.Features.ReferenceData;

[ApiController]
[Authorize]
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
    [AllowAnonymous]
    public IActionResult GetAllCountries() => Ok(CountryCatalog.All);

    [HttpGet("GetPfdCountries")]
    [AllowAnonymous]
    public IActionResult GetPfdCountries() => Ok(CountryCatalog.PreparationForDelivery);

    [HttpGet("GetAllFailureReasons")]
    [AllowAnonymous]
    public IActionResult GetAllFailureReasons() => Ok(FailureReasonCatalog.All);

    [HttpGet("GetCitiesByCountry")]
    [AllowAnonymous]
    public IActionResult GetCitiesByCountry(
        [FromQuery(Name = "countryIds")] int[]? countryIds) =>
        Ok(CountryCityCatalog.GetDistinctCities(countryIds));

    [HttpGet("GetAllOrderSources")]
    [HttpPost("GetAllOrderSources")]
    public IActionResult GetAllOrderSources() => Ok(OrderSourceCatalog.All);

    [HttpGet("GetAllOrderStatuses")]
    public IActionResult GetAllOrderStatuses()
    {
        if (User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector"))
            return Ok(OrderStatusCatalog.Administrators);
        if (User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative"))
            return Ok(OrderStatusCatalog.Delivery);
        if (User.IsInRole("CallCenter")) return Ok(OrderStatusCatalog.CallCenter);
        if (User.IsInRole("FollowUpDepartment")) return Ok(OrderStatusCatalog.FollowUp);
        return Forbid();
    }

    [HttpGet("GetAllDeliveryCompanies")]
    [HttpPost("GetAllDeliveryCompanies")]
    public async Task<IActionResult> GetAllDeliveryCompanies(
        [FromQuery] int? countryId,
        [FromQuery] int[]? countryIds,
        CancellationToken ct)
    {
        var query = _context.DeliveryCompanies.AsNoTracking().Where(item => item.IsShown && !item.IsRepresentative);
        var countries = (countryIds ?? []).Where(id => id > 0).Distinct().ToList();
        if (countryId is > 0) countries.Add(countryId.Value);
        if (countries.Count > 0) query = query.Where(item => countries.Contains(item.Country));

        var list = await query.Select(d => new { id = d.Id, name = d.Name, logoUrl = d.ImageUrl ?? "/static/DefaultImage.svg" }).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("GetAllEmployees")]
    [HttpPost("GetAllEmployees")]
    public async Task<IActionResult> GetAllEmployees(CancellationToken ct) => Ok(await _context.Employees.AsNoTracking()
        .Where(item => item.IsShown)
        .Select(item => new { Id = item.ApplicationUserId, Name = item.Name, LogoUrl = item.ImageUrl ?? "static/DefaultImage.svg" })
        .ToListAsync(ct));

    [HttpGet("GetAllEmployeesintId")]
    [HttpPost("GetAllEmployeesintId")]
    public async Task<IActionResult> GetAllEmployeesIntId(CancellationToken ct) => Ok(await _context.Employees.AsNoTracking()
        .Where(item => item.IsShown)
        .Select(item => new { item.Id, Name = item.Name, LogoUrl = item.ImageUrl ?? "static/DefaultImage.svg" })
        .ToListAsync(ct));

    [HttpGet("GetAllStores")]
    public async Task<IActionResult> GetAllStores(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _context.ManufacturingCompanies.AsNoTracking().Where(item => item.IsShown);
        if (User.IsInRole("FollowUpDepartment") || User.IsInRole("CallCenter"))
            query = query.Where(company => _context.EmployeeManufacturingCompanies.Any(access =>
                access.ManufacturingCompanyId == company.Id && access.ApplicationUserId == userId && access.CanSeeManufacturingCompany));
        return Ok(await query.Select(item => new
        {
            id = item.Id,
            name = item.Name,
            logoUrl = item.ImageUrl ?? "static/DefaultImage.svg",
            mainWarehouseId = item.MainWarehouseId,
        }).ToListAsync(ct));
    }

    [HttpGet("GetAllDeliveryRepresentatives")]
    [HttpPost("GetAllDeliveryRepresentatives")]
    public async Task<IActionResult> GetAllDeliveryRepresentatives(
        [FromQuery] int[]? countryIds,
        [FromQuery] string[]? cityIds,
        CancellationToken ct)
    {
        var query = _context.DeliveryCompanies.AsNoTracking().Where(item => item.IsShown && item.IsRepresentative);
        var countries = (countryIds ?? []).Where(id => id > 0).Distinct().ToList();
        var cities = (cityIds ?? []).Where(city => !string.IsNullOrWhiteSpace(city)).Distinct().ToList();
        if (countries.Count > 0) query = query.Where(item => countries.Contains(item.Country));
        if (cities.Count > 0) query = query.Where(item => item.City != null && cities.Contains(item.City));
        return Ok(await query.Select(item => new { item.Id, item.Name, LogoUrl = item.ImageUrl ?? "static/DefaultImage.svg" }).ToListAsync(ct));
    }

    [HttpGet("GetAllDeliveryCompaniesAndRepresentatives")]
    [HttpPost("GetAllDeliveryCompaniesAndRepresentatives")]
    public async Task<IActionResult> GetAllDeliveryCompaniesAndRepresentatives(
        [FromQuery] int? countryId,
        [FromQuery] string? cityId,
        [FromQuery] int? orderId,
        CancellationToken ct)
    {
        var query = _context.DeliveryCompanies.AsNoTracking().Where(item => item.IsShown);
        if (User.IsInRole("CallCenter") && orderId.HasValue)
        {
            var storeId = await _context.Orders.AsNoTracking().Where(item => item.Id == orderId.Value)
                .Select(item => item.ManufacturingCompanyId).FirstOrDefaultAsync(ct);
            if (!storeId.HasValue) return Ok(Array.Empty<object>());
            var assignment = await _context.StoreDeliveryCompanyAssignments.AsNoTracking()
                .FirstOrDefaultAsync(item => item.ManufacturingCompanyId == storeId.Value, ct);
            if (assignment is null || assignment.IsManualTransfer || !assignment.DeliveryCompanyId.HasValue)
                return Ok(Array.Empty<object>());
            query = query.Where(item => item.Id == assignment.DeliveryCompanyId.Value);
        }
        if (countryId.HasValue) query = query.Where(item => item.Country == countryId.Value);
        if (!string.IsNullOrWhiteSpace(cityId))
            query = query.Where(item => !item.IsRepresentative || item.City == cityId);
        return Ok(await query.OrderBy(item => item.IsRepresentative).ThenBy(item => item.Name)
            .Select(item => new { item.Id, item.Name, LogoUrl = item.ImageUrl ?? "static/DefaultImage.svg", item.IsRepresentative })
            .ToListAsync(ct));
    }

    [HttpPost("GetDeliveryPrice")]
    public async Task<IActionResult> GetDeliveryPrice(
        [FromQuery] int deliveryCompanyId,
        [FromQuery] int countryId,
        [FromQuery] string? cityId,
        CancellationToken ct)
    {
        var price = await _context.DeliveryCompanyPrices.AsNoTracking()
            .Where(item => item.DeliveryCompanyId == deliveryCompanyId && item.Country == countryId &&
                           (item.City == null || item.City == cityId || cityId == null))
            .OrderByDescending(item => item.City == cityId)
            .Select(item => (decimal?)item.Price).FirstOrDefaultAsync(ct);
        return Ok(new { price = price ?? 0 });
    }

    [HttpGet("GetFilteredWarehouses")]
    [HttpPost("GetFilteredWarehouses")]
    public async Task<IActionResult> GetFilteredWarehouses([FromQuery] int? deliveryCompanyId, CancellationToken ct)
    {
        var query = _context.Warehouses.AsNoTracking().Where(item => item.IsShown && item.Amount > 0);
        if (deliveryCompanyId.HasValue) query = query.Where(item => item.DeliveryCompanyId == deliveryCompanyId.Value);
        return Ok(await query.Select(item => new
        {
            id = item.Id,
            name = item.Name,
            productImage = item.MainWarehouse != null ? item.MainWarehouse.ImageUrl ?? "static/DefaultImage.svg" : "static/DefaultImage.svg",
            amount = item.Amount,
            mainWarehouseId = item.MainWarehouseId,
        }).ToListAsync(ct));
    }

    [HttpGet("GetMainWarehouses")]
    [HttpPost("GetMainWarehouses")]
    public async Task<IActionResult> GetMainWarehouses(CancellationToken ct) => Ok(await _context.MainWarehouses.AsNoTracking()
        .Select(item => new { item.Id, item.Name, LogoUrl = item.ImageUrl ?? "static/DefaultImage.svg" }).ToListAsync(ct));

    [HttpGet("GetSubWarehouses")]
    [HttpPost("GetSubWarehouses")]
    public async Task<IActionResult> GetSubWarehouses([FromQuery] int? mainWarehouseId, CancellationToken ct)
    {
        var query = _context.SubWarehouses.AsNoTracking().AsQueryable();
        if (mainWarehouseId.HasValue) query = query.Where(item => item.MainWarehouseId == mainWarehouseId.Value);
        return Ok(await query.Select(item => new { item.Id, item.Name }).ToListAsync(ct));
    }

    [HttpGet("GetCampaignsByCountry")]
    [HttpPost("GetCampaignsByCountry")]
    public async Task<IActionResult> GetCampaignsByCountry([FromQuery] int countryId, CancellationToken ct) => Ok(await _context.AdvertisingCampaigns.AsNoTracking()
        .Where(item => item.Country == countryId && item.IsActive)
        .OrderBy(item => item.Name)
        .Select(item => new { item.Id, item.ImageUrl, item.Name, item.ManufacturingCompanyId })
        .ToListAsync(ct));

    [HttpGet("GetAssignableUsers")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> GetAssignableUsers(CancellationToken ct)
    {
        string[] targetRoles = ["CallCenter", "FollowUpDepartment"];
        var query = from role in _context.Roles.AsNoTracking()
                    join userRole in _context.UserRoles.AsNoTracking() on role.Id equals userRole.RoleId
                    join user in _context.Users.AsNoTracking() on userRole.UserId equals user.Id
                    join employee in _context.Employees.AsNoTracking().Where(item => item.IsShown)
                        on user.Id equals employee.ApplicationUserId
                    where role.Name != null && targetRoles.Contains(role.Name)
                    select new { user.Id, Name = employee.DisplayName ?? employee.Name };
        return Ok(await query.Distinct().OrderBy(item => item.Name).ToListAsync(ct));
    }
}
