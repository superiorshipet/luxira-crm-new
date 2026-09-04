using System.Text.Json;
using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Binding;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
[Route("api/v1/marketing/advertising")]
[Route("AdvertisingManager")]
public sealed class AdvertisingManagerController(ApplicationDbContext context, IDataProtectionProvider protection) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private readonly IDataProtector _cardProtector = protection.CreateProtector("lotus_blue.AdvertisingManager.PaymentCardNumber.v1");
    private readonly IDataProtector _cvvProtector = protection.CreateProtector("lotus_blue.AdvertisingManager.PaymentCardCvv.v1");

    [HttpGet("GetCampaigns")]
    public async Task<IActionResult> GetCampaigns(int? country, CancellationToken ct) { var query = context.AdvertisingCampaigns.AsNoTracking(); if (country is > 0) query = query.Where(item => item.Country == country); return Ok(await query.OrderByDescending(item => item.CreatedAt).ToListAsync(ct)); }

    [HttpPost("CreateCampaign")]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateAdCampaignRequest request, CancellationToken ct) { var item = new AdvertisingCampaign { Name = request.Name.Trim(), Country = request.Country, MainWarehouseId = request.MainWarehouseId, ManufacturingCompanyId = request.ManufacturingCompanyId, IsActive = true, CreatedAt = IstanbulTimeHelper.Now }; context.AdvertisingCampaigns.Add(item); await context.SaveChangesAsync(ct); return Ok(item); }

    [HttpGet("GetCampaignRoi/{campaignId:int}")]
    public async Task<IActionResult> GetCampaignRoi([RouteOrRequest] int campaignId, CancellationToken ct) { var campaign = await context.AdvertisingCampaigns.FindAsync([campaignId], ct); if (campaign is null) return NotFound(); var query = context.Orders.AsNoTracking().Where(item => item.CampaignId == campaignId); return Ok(new CampaignRoiDto(campaign.Id, campaign.Name ?? "", await query.CountAsync(ct), await query.CountAsync(item => item.OrderStatus == OrderStatusCodes.Delivered, ct), await query.Where(item => item.OrderStatus == OrderStatusCodes.Delivered).SumAsync(item => item.TotalPrice, ct))); }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/AdvertisingManager/Index")]
    public Task<IActionResult> Index(int? storeId, string? accountName, string? folderName, string? facebookPageName, string? email, CancellationToken ct) => Dashboard(storeId, accountName, folderName, facebookPageName, email, ct);

    [HttpGet("Board")]
    [HttpGet("/AdvertisingManager/Board")]
    public Task<IActionResult> Board(CancellationToken ct) => Dashboard(null, null, null, null, null, ct);

    [HttpGet("Accounts")]
    [HttpGet("/AdvertisingManager/Accounts")]
    public Task<IActionResult> Accounts(CancellationToken ct) => Dashboard(null, null, null, null, null, ct);

    [HttpGet("StoresFacebook")]
    [HttpGet("/AdvertisingManager/StoresFacebook")]
    public Task<IActionResult> StoresFacebook(int? storeId, string? facebookPageName, string? email, string? search, CancellationToken ct) => Dashboard(storeId, search, null, facebookPageName, email, ct);

    [HttpPost("Create")]
    [HttpPost("/AdvertisingManager/Create")]
    public async Task<IActionResult> Create([FromBody] AdvertisingItemCreateRequest request, CancellationToken ct)
    {
        if (request.ManufacturingCompanyId <= 0 || request.StorePasswordPageId <= 0) return BadRequest(new { success = false, message = "اختر المتجر والصفحة." });
        var store = await context.ManufacturingCompanies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.ManufacturingCompanyId && item.IsShown, ct); var page = await context.StorePasswordPages.AsNoTracking().Include(item => item.PasswordPageType).FirstOrDefaultAsync(item => item.Id == request.StorePasswordPageId && !item.IsDeleted, ct); if (store is null || page is null) return BadRequest(new { success = false, message = "المتجر أو الصفحة غير صحيح." });
        var instagram = IsInstagram(page.PasswordPageType?.Name, page.PasswordPageType?.IconClass); if (!instagram && string.IsNullOrWhiteSpace(request.FolderName)) return BadRequest(new { success = false, message = "اسم الحافظة مطلوب عند إضافة Facebook." });
        var folder = await context.AdvertisingManagerStoreFolders.FirstOrDefaultAsync(item => item.ManufacturingCompanyId == request.ManufacturingCompanyId, ct);
        if (folder is null) { var max = await context.AdvertisingManagerStoreFolders.MaxAsync(item => (int?)item.SortOrder, ct) ?? -1; folder = new AdvertisingManagerStoreFolder { ManufacturingCompanyId = request.ManufacturingCompanyId, SortOrder = max + 1, CreatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId() }; context.AdvertisingManagerStoreFolders.Add(folder); await context.SaveChangesAsync(ct); }
        var item = new AdvertisingManagerItem { AdvertisingManagerStoreFolderId = folder.Id, StorePasswordPageId = page.Id, FolderName = instagram ? "" : request.FolderName.Trim(), AccountName = instagram ? null : Clean(request.AccountName), FacebookPageNameSnapshot = page.PageName, EmailSnapshot = Clean(page.Email), PasswordSnapshot = page.Password, CreatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId() };
        context.AdvertisingManagerItems.Add(item); await context.SaveChangesAsync(ct); return Ok(new { success = true, id = item.Id, storeFolderId = folder.Id, platform = instagram ? "instagram" : "facebook" });
    }

    [HttpPost("ReorderStores")]
    [HttpPost("/AdvertisingManager/ReorderStores")]
    public async Task<IActionResult> ReorderStores([FromForm] string? orderedFolderIds, CancellationToken ct) { var ids = (orderedFolderIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(value => int.TryParse(value, out var id) ? id : 0).Where(id => id > 0).Distinct().ToArray(); var rows = await context.AdvertisingManagerStoreFolders.Where(item => ids.Contains(item.Id)).ToListAsync(ct); if (rows.Count != ids.Length) return BadRequest(new { success = false, message = "ترتيب المتاجر المرسل غير صحيح." }); for (var i = 0; i < ids.Length; i++) rows.Single(item => item.Id == ids[i]).SortOrder = i; await context.SaveChangesAsync(ct); return Ok(new { success = true, tickets = ids.Select((id, index) => new { storeFolderId = id, ticketNumber = index + 1 }) }); }

    [HttpPost("UpdateStore")]
    [HttpPost("/AdvertisingManager/UpdateStore")]
    public async Task<IActionResult> UpdateStore([FromForm] int storeFolderId, [FromForm] int manufacturingCompanyId, CancellationToken ct) { var source = await context.AdvertisingManagerStoreFolders.FindAsync([storeFolderId], ct); if (source is null || !await context.ManufacturingCompanies.AnyAsync(item => item.Id == manufacturingCompanyId && item.IsShown, ct)) return NotFound(); var destination = await context.AdvertisingManagerStoreFolders.FirstOrDefaultAsync(item => item.ManufacturingCompanyId == manufacturingCompanyId && item.Id != storeFolderId, ct); if (destination is null) source.ManufacturingCompanyId = manufacturingCompanyId; else { await context.AdvertisingManagerItems.Where(item => item.AdvertisingManagerStoreFolderId == source.Id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AdvertisingManagerStoreFolderId, destination.Id), ct); context.AdvertisingManagerStoreFolders.Remove(source); } await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    [HttpPost("DeleteStore")]
    [HttpPost("/AdvertisingManager/DeleteStore")]
    public async Task<IActionResult> DeleteStore([FromForm] int storeFolderId, CancellationToken ct) { var folder = await context.AdvertisingManagerStoreFolders.FindAsync([storeFolderId], ct); if (folder is null) return NotFound(); context.AdvertisingManagerStoreFolders.Remove(folder); await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    [HttpPost("AddAccount")]
    [HttpPost("/AdvertisingManager/AddAccount")]
    public Task<IActionResult> AddAccount([FromBody] AdvertisingAccountRequest request, CancellationToken ct) => SaveAccount(request with { AccountId = null }, false, ct);

    [HttpPost("UpdateAccount")]
    [HttpPost("/AdvertisingManager/UpdateAccount")]
    public Task<IActionResult> UpdateAccount([FromBody] AdvertisingAccountRequest request, CancellationToken ct) => SaveAccount(request, true, ct);

    [HttpGet("GetAccountDetails")]
    [HttpGet("/AdvertisingManager/GetAccountDetails")]
    public async Task<IActionResult> GetAccountDetails(int itemId, int? accountId, CancellationToken ct) { var key = AccountKey(itemId, accountId); var profile = await context.AdvertisingManagerAccountProfiles.AsNoTracking().Include(item => item.Links).Include(item => item.PaymentCards).FirstOrDefaultAsync(item => item.AdvertisingManagerItemId == itemId && item.AccountKey == key, ct); return Ok(new { success = true, accountStatus = profile?.AccountStatus ?? "Active", links = profile?.Links.OrderBy(item => item.SortOrder).Select(item => new { item.Id, name = item.LinkName, url = item.LinkUrl }), paymentCards = profile?.PaymentCards.OrderBy(item => item.SortOrder).Select(item => new { item.Id, item.CardholderName, last4 = item.CardLast4, item.CardBrand, item.ExpiryMonth, item.ExpiryYear, item.CardType }) }); }

    [HttpGet("GetPaymentCardNumber")]
    [HttpGet("/AdvertisingManager/GetPaymentCardNumber")]
    public async Task<IActionResult> GetPaymentCardNumber(int id, CancellationToken ct) { var card = await context.AdvertisingManagerPaymentCards.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct); if (card is null) return NotFound(); try { return Ok(new { success = true, cardholderName = card.CardholderName, cardNumber = card.CardNumberProtected is null ? null : _cardProtector.Unprotect(card.CardNumberProtected), cvv = card.CardCvvProtected is null ? null : _cvvProtector.Unprotect(card.CardCvvProtected), card.CardBrand, card.ExpiryMonth, card.ExpiryYear, card.CardType }); } catch { return StatusCode(500, new { success = false, message = "تعذر فك بيانات البطاقة." }); } }

    [HttpPost("DeleteAccount")]
    [HttpPost("/AdvertisingManager/DeleteAccount")]
    public async Task<IActionResult> DeleteAccount([FromForm] int itemId, [FromForm] int? accountId, CancellationToken ct) { if (accountId is > 0) { var account = await context.AdvertisingManagerItemAccounts.FirstOrDefaultAsync(item => item.Id == accountId && item.AdvertisingManagerItemId == itemId, ct); if (account is null) return NotFound(); context.AdvertisingManagerItemAccounts.Remove(account); } else { var item = await context.AdvertisingManagerItems.FindAsync([itemId], ct); if (item is null) return NotFound(); item.AccountName = null; } var key = AccountKey(itemId, accountId); await context.AdvertisingManagerAccountProfiles.Where(item => item.AccountKey == key).ExecuteDeleteAsync(ct); await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    [HttpGet("GetItem")]
    [HttpGet("/AdvertisingManager/GetItem")]
    public async Task<IActionResult> GetItem(int id, CancellationToken ct) { var item = await context.AdvertisingManagerItems.AsNoTracking().Include(row => row.StorePasswordPage).FirstOrDefaultAsync(row => row.Id == id && !row.IsDeleted, ct); return item is null ? NotFound() : Ok(new { success = true, item = new { item.Id, item.StorePasswordPageId, pageName = item.StorePasswordPage?.PageName ?? item.FacebookPageNameSnapshot, email = item.StorePasswordPage?.Email ?? item.EmailSnapshot, password = item.StorePasswordPage?.Password ?? item.PasswordSnapshot, item.FolderName, item.AccountName } }); }

    [HttpPost("UpdateField")]
    [HttpPost("/AdvertisingManager/UpdateField")]
    public async Task<IActionResult> UpdateField([FromForm] int id, [FromForm] string fieldName, [FromForm] string? value, [FromForm] int? storePasswordPageId, CancellationToken ct) { var item = await context.AdvertisingManagerItems.FirstOrDefaultAsync(row => row.Id == id && !row.IsDeleted, ct); if (item is null) return NotFound(); switch (fieldName.Trim().ToLowerInvariant()) { case "page": var page = await context.StorePasswordPages.AsNoTracking().FirstOrDefaultAsync(row => row.Id == storePasswordPageId && !row.IsDeleted, ct); if (page is null) return BadRequest(); item.StorePasswordPageId = page.Id; item.FacebookPageNameSnapshot = page.PageName; item.EmailSnapshot = page.Email; item.PasswordSnapshot = page.Password; break; case "folder": if (string.IsNullOrWhiteSpace(value)) return BadRequest(); item.FolderName = value.Trim(); break; case "account": if (string.IsNullOrWhiteSpace(value)) return BadRequest(); item.AccountName = value.Trim(); break; default: return BadRequest(new { success = false, message = "نوع التعديل غير صحيح." }); } item.UpdatedAt = IstanbulTimeHelper.Now; item.UpdatedByUserId = User.GetUserId(); await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    [HttpPost("DeleteField")]
    [HttpPost("/AdvertisingManager/DeleteField")]
    public async Task<IActionResult> DeleteField([FromForm] int id, [FromForm] string fieldName, CancellationToken ct) { var item = await context.AdvertisingManagerItems.FindAsync([id], ct); if (item is null || item.IsDeleted) return NotFound(); switch (fieldName.Trim().ToLowerInvariant()) { case "account": item.AccountName = null; break; case "page": case "folder": item.AccountName = null; item.IsDeleted = true; item.DeletedAt = IstanbulTimeHelper.Now; item.DeletedByUserId = User.GetUserId(); break; default: return BadRequest(); } item.UpdatedAt = IstanbulTimeHelper.Now; item.UpdatedByUserId = User.GetUserId(); await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    [HttpPost("DeleteItem")]
    [HttpPost("/AdvertisingManager/DeleteItem")]
    public Task<IActionResult> DeleteItem([FromForm] int id, CancellationToken ct) => DeleteField(id, "page", ct);

    private async Task<IActionResult> Dashboard(int? storeId, string? accountName, string? folderName, string? pageName, string? email, CancellationToken ct)
    {
        var query = context.AdvertisingManagerItems.AsNoTracking().Where(item => !item.IsDeleted).Include(item => item.StoreFolder).ThenInclude(folder => folder!.ManufacturingCompany).Include(item => item.StorePasswordPage).AsSplitQuery();
        if (storeId is > 0) query = query.Where(item => item.StoreFolder!.ManufacturingCompanyId == storeId); if (!string.IsNullOrWhiteSpace(accountName)) query = query.Where(item => item.AccountName != null && item.AccountName.Contains(accountName)); if (!string.IsNullOrWhiteSpace(folderName)) query = query.Where(item => item.FolderName != null && item.FolderName.Contains(folderName)); if (!string.IsNullOrWhiteSpace(pageName)) query = query.Where(item => item.FacebookPageNameSnapshot.Contains(pageName)); if (!string.IsNullOrWhiteSpace(email)) query = query.Where(item => item.EmailSnapshot != null && item.EmailSnapshot.Contains(email));
        var items = await query.OrderBy(item => item.StoreFolder!.SortOrder).ThenBy(item => item.CreatedAt).Take(1000).ToListAsync(ct); var ids = items.Select(item => item.Id).ToArray(); var accounts = await context.AdvertisingManagerItemAccounts.AsNoTracking().Where(item => ids.Contains(item.AdvertisingManagerItemId)).ToListAsync(ct); return Ok(new { totalStores = items.Select(item => item.AdvertisingManagerStoreFolderId).Distinct().Count(), totalPages = items.Count, totalFolders = items.Count(item => !string.IsNullOrWhiteSpace(item.FolderName)), totalAccounts = items.Count(item => !string.IsNullOrWhiteSpace(item.AccountName)) + accounts.Count, items, additionalAccounts = accounts });
    }

    private async Task<IActionResult> SaveAccount(AdvertisingAccountRequest request, bool edit, CancellationToken ct)
    {
        var item = await context.AdvertisingManagerItems.FirstOrDefaultAsync(row => row.Id == request.ItemId && !row.IsDeleted, ct); if (item is null || string.IsNullOrWhiteSpace(request.AccountName)) return BadRequest(new { success = false, message = "الحساب أو الاسم غير صحيح." });
        int? accountId = request.AccountId; if (edit && accountId is > 0) { var account = await context.AdvertisingManagerItemAccounts.FirstOrDefaultAsync(row => row.Id == accountId && row.AdvertisingManagerItemId == item.Id, ct); if (account is null) return NotFound(); account.AccountName = request.AccountName.Trim(); account.UpdatedAt = IstanbulTimeHelper.Now; account.UpdatedByUserId = User.GetUserId(); } else if (!edit) { var account = new AdvertisingManagerItemAccount { AdvertisingManagerItemId = item.Id, AccountName = request.AccountName.Trim(), CreatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId() }; context.AdvertisingManagerItemAccounts.Add(account); await context.SaveChangesAsync(ct); accountId = account.Id; } else item.AccountName = request.AccountName.Trim();
        var key = AccountKey(item.Id, accountId); var profile = await context.AdvertisingManagerAccountProfiles.Include(row => row.Links).Include(row => row.PaymentCards).FirstOrDefaultAsync(row => row.AccountKey == key, ct); if (profile is null) { profile = new AdvertisingManagerAccountProfile { AdvertisingManagerItemId = item.Id, AccountKey = key, CreatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId() }; context.AdvertisingManagerAccountProfiles.Add(profile); } profile.AccountStatus = request.AccountStatus == "Inactive" ? "Inactive" : "Active"; profile.UpdatedAt = IstanbulTimeHelper.Now; profile.UpdatedByUserId = User.GetUserId(); context.AdvertisingManagerAccountLinks.RemoveRange(profile.Links); context.AdvertisingManagerPaymentCards.RemoveRange(profile.PaymentCards);
        var links = Parse<List<AdvertisingLinkRequest>>(request.LinksJson) ?? request.Links ?? []; var cards = Parse<List<AdvertisingCardRequest>>(request.PaymentCardsJson) ?? request.PaymentCards ?? []; var order = 0; foreach (var link in links.Where(link => !string.IsNullOrWhiteSpace(link.Name) && Uri.TryCreate(link.Url, UriKind.Absolute, out _))) profile.Links.Add(new AdvertisingManagerAccountLink { LinkName = link.Name!.Trim(), LinkUrl = link.Url!.Trim(), SortOrder = order++, CreatedAt = IstanbulTimeHelper.Now }); order = 0; foreach (var card in cards.Where(card => card.ExpiryMonth is >= 1 and <= 12 && card.ExpiryYear > 0)) { var digits = new string((card.CardNumber ?? "").Where(char.IsDigit).ToArray()); profile.PaymentCards.Add(new AdvertisingManagerPaymentCard { CardholderName = card.CardholderName?.Trim() ?? "", CardLast4 = digits.Length >= 4 ? digits[^4..] : (card.Last4 ?? "").Trim(), CardNumberProtected = digits.Length == 0 ? null : _cardProtector.Protect(digits), CardCvvProtected = string.IsNullOrWhiteSpace(card.Cvv) ? null : _cvvProtector.Protect(card.Cvv.Trim()), CardBrand = card.CardBrand?.Trim() ?? "Card", ExpiryMonth = card.ExpiryMonth, ExpiryYear = card.ExpiryYear, CardType = card.CardType == "Default" ? "Default" : "Backup", SortOrder = order++, CreatedAt = IstanbulTimeHelper.Now }); } await context.SaveChangesAsync(ct); return Ok(new { success = true, accountId });
    }

    private static string AccountKey(int itemId, int? accountId) => accountId is > 0 ? $"A:{accountId}" : $"P:{itemId}";
    private static bool IsInstagram(string? name, string? icon) => (name ?? "").Contains("instagram", StringComparison.OrdinalIgnoreCase) || (icon ?? "").Contains("instagram", StringComparison.OrdinalIgnoreCase);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static T? Parse<T>(string? json) { if (string.IsNullOrWhiteSpace(json)) return default; try { return JsonSerializer.Deserialize<T>(json, JsonOptions); } catch (JsonException) { return default; } }
}

public sealed record AdvertisingItemCreateRequest(int ManufacturingCompanyId, int StorePasswordPageId, string FolderName, string? AccountName);
public sealed record AdvertisingAccountRequest(int ItemId, int? AccountId, string AccountName, string AccountStatus = "Active", string? LinksJson = null, string? PaymentCardsJson = null, List<AdvertisingLinkRequest>? Links = null, List<AdvertisingCardRequest>? PaymentCards = null);
public sealed record AdvertisingLinkRequest(int? Id, string? Name, string? Url);
public sealed record AdvertisingCardRequest(int? Id, string? CardholderName, string? CardNumber, string? Last4, string? CardBrand, int ExpiryMonth, int ExpiryYear, string? Cvv, string? CardType);
public sealed record CreateAdCampaignRequest(string Name, int Country, int? MainWarehouseId, int? ManufacturingCompanyId);
public sealed record CampaignRoiDto(int CampaignId, string CampaignName, int TotalOrders, int DeliveredOrders, decimal Revenue);
