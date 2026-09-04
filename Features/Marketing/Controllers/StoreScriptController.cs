using System.Text.Json;
using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/marketing/scripts")]
[Route("StoreScript")]
public sealed class StoreScriptController(ApplicationDbContext context, IWebHostEnvironment environment) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> Platforms = ["WhatsApp", "Meta"];
    private static readonly HashSet<string> IconKinds = ["emoji", "twemoji", "svg"];
    private static readonly HashSet<string> TargetKinds = ["AssetId", "PageId", "BusinessId"];

    [HttpGet("GetScripts")]
    public async Task<IActionResult> GetScripts(int? manufacturingCompanyId, string? platform, CancellationToken ct)
    {
        var query = context.StoreScripts.AsNoTracking().Where(item => !item.IsDeleted);
        if (manufacturingCompanyId is > 0) query = query.Where(item => item.ManufacturingCompanyId == manufacturingCompanyId);
        if (!string.IsNullOrWhiteSpace(platform)) query = query.Where(item => item.Platform == platform);
        return Ok(await query.OrderByDescending(item => item.UpdatedAt).Take(200).ToListAsync(ct));
    }

    [AllowAnonymous]
    [HttpGet("/seedscript/manifest")]
    public async Task<IActionResult> Manifest(string? platform, string? assetId, string? pageId, string? businessId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        var canonical = Platforms.FirstOrDefault(value => string.Equals(value, platform, StringComparison.OrdinalIgnoreCase));
        if (canonical is null) return Ok(new { scripts = Array.Empty<object>() });
        var query = context.StoreScripts.AsNoTracking().Where(item => item.Platform == canonical && item.IsActive && !item.IsDeleted);
        if (!CanManage())
        {
            var userId = User.GetUserId() ?? string.Empty;
            var allowed = context.EmployeeManufacturingCompanies.Where(item => item.ApplicationUserId == userId && item.CanSeeManufacturingCompany).Select(item => item.ManufacturingCompanyId);
            query = query.Where(item => allowed.Contains(item.ManufacturingCompanyId));
        }
        var candidates = await query.Select(item => new { Script = item, Targets = item.Targets.Where(target => !target.IsDeleted).ToList() }).ToListAsync(ct);
        var matchedOn = "Permission";
        if (canonical == "Meta" && !string.IsNullOrWhiteSpace(assetId)) { candidates = candidates.Where(item => item.Targets.Any(target => target.Kind == "AssetId" && target.Value == assetId)).ToList(); if (candidates.Count == 1) matchedOn = "AssetId"; }
        if (candidates.Count > 1 && !string.IsNullOrWhiteSpace(pageId)) { var narrowed = candidates.Where(item => item.Targets.Any(target => target.Kind == "PageId" && target.Value == pageId)).ToList(); if (narrowed.Count > 0) { candidates = narrowed; if (narrowed.Count == 1) matchedOn = "PageId"; } }
        if (candidates.Count > 1 && !string.IsNullOrWhiteSpace(businessId)) { var narrowed = candidates.Where(item => item.Targets.Any(target => target.Kind == "BusinessId" && target.Value == businessId)).ToList(); if (narrowed.Count > 0) { candidates = narrowed; if (narrowed.Count == 1) matchedOn = "BusinessId"; } }
        var companyIds = candidates.Select(item => item.Script.ManufacturingCompanyId).Distinct().ToArray();
        var companies = await context.ManufacturingCompanies.AsNoTracking().Where(item => companyIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, ct);
        return Ok(new { scripts = candidates.Select(item => new { scriptId = item.Script.Id, storeId = item.Script.ManufacturingCompanyId, storeName = companies.GetValueOrDefault(item.Script.ManufacturingCompanyId)?.Name ?? "", storeLogoUrl = companies.GetValueOrDefault(item.Script.ManufacturingCompanyId)?.ImageUrl ?? "", platform = item.Script.Platform, revision = item.Script.RevisionStamp, matchedOn }) });
    }

    [AllowAnonymous]
    [HttpGet("/seedscript/definition/{scriptId:int}")]
    public async Task<IActionResult> Definition(int scriptId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        var script = await LoadTree(scriptId, liveOnly: true, ct); if (script is null) return NotFound();
        if (!await CanAccess(script.ManufacturingCompanyId, ct)) return StatusCode(403);
        return Ok(Tree(script, liveOnly: true));
    }

    [AllowAnonymous]
    [HttpGet("/seedscript/engine.js")]
    public async Task<IActionResult> EngineScript(int? scriptId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store"; if (User.Identity?.IsAuthenticated != true) return Unauthorized(); if (!scriptId.HasValue) return BadRequest("scriptId is required");
        var script = await context.StoreScripts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == scriptId && item.IsActive && !item.IsDeleted, ct); if (script is null) return NotFound(); if (!await CanAccess(script.ManufacturingCompanyId, ct)) return StatusCode(403);
        return await ScriptFile("luxira-engine.js", ct);
    }

    [AllowAnonymous]
    [HttpGet("/seedscript/loader.user.js")]
    public async Task<IActionResult> LoaderScript(CancellationToken ct) { Response.Headers.CacheControl = "no-store"; if (User.Identity?.IsAuthenticated != true) return Unauthorized(); return await ScriptFile("luxira-loader.user.js", ct); }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpGet("/StoreScript/GetScriptDashboardAjax/{folderId:int}")]
    public async Task<IActionResult> GetScriptDashboardAjax(int folderId, string? platform, CancellationToken ct)
    {
        var folder = await context.StoreCodeFolders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == folderId && !item.IsDeleted, ct); if (folder is null) return BadRequest(new { message = "المجلد غير موجود." });
        var script = await context.StoreScripts.FirstOrDefaultAsync(item => item.StoreCodeFolderId == folderId, ct);
        if (script is null)
        {
            var canonical = Platforms.FirstOrDefault(value => string.Equals(value, platform, StringComparison.OrdinalIgnoreCase));
            if (canonical is null) return Ok(new { success = true, exists = false, folderId = folder.Id, folderName = folder.FolderName, manufacturingCompanyId = folder.ManufacturingCompanyId });
            script = new StoreScript { StoreCodeFolderId = folder.Id, ManufacturingCompanyId = folder.ManufacturingCompanyId, Platform = canonical, EngineVersion = "1.0.0", RevisionStamp = 1, CreatedAt = IstanbulTimeHelper.Now, UpdatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name, UpdatedByUserId = User.GetUserId(), UpdatedByName = User.Identity?.Name };
            context.StoreScripts.Add(script); await context.SaveChangesAsync(ct); History(script.Id, "ScriptDefinition", script.Id, "Created", null, canonical); await context.SaveChangesAsync(ct);
        }
        script = await LoadTree(script.Id, false, ct);
        return Ok(new { success = true, exists = true, folderId = folder.Id, folderName = folder.FolderName, manufacturingCompanyId = folder.ManufacturingCompanyId, definition = Tree(script!, false) });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SaveCategoryAjax")]
    public async Task<IActionResult> SaveCategoryAjax([FromForm] int folderId, [FromForm] int id, [FromForm] string key, [FromForm] string label, [FromForm] string icon, [FromForm] string iconKind, [FromForm] int sortOrder, CancellationToken ct)
    {
        var error = ValidateNode(key, label, icon, iconKind, 200); if (error is not null) return BadRequest(new { message = error });
        var script = await ByFolder(folderId, ct); if (script is null) return BadRequest(new { message = "افتح لوحة تحكم السكربت أولاً لإنشائه." });
        var item = id > 0 ? await context.ScriptCategories.FirstOrDefaultAsync(row => row.Id == id && row.ScriptDefinitionId == script.Id, ct) : null;
        if (id > 0 && item is null) return BadRequest(new { message = "التصنيف غير موجود." });
        if (item is null) { item = new ScriptCategory { ScriptDefinitionId = script.Id, CreatedAt = IstanbulTimeHelper.Now, IsEnabled = true, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name }; context.ScriptCategories.Add(item); }
        item.Key = key.Trim(); item.Label = label.Trim(); item.Icon = icon.Trim(); item.IconKind = iconKind.Trim().ToLowerInvariant(); item.SortOrder = sortOrder; Touch(item); Bump(script); await context.SaveChangesAsync(ct);
        return Ok(new { success = true, id = item.Id, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SaveSubCategoryAjax")]
    public async Task<IActionResult> SaveSubCategoryAjax([FromForm] int categoryId, [FromForm] int? parentSubCategoryId, [FromForm] int id, [FromForm] string key, [FromForm] string label, [FromForm] string icon, [FromForm] string iconKind, [FromForm] string? colorToken, [FromForm] bool isCountryScoped, [FromForm] int sortOrder, CancellationToken ct)
    {
        var error = ValidateNode(key, label, icon, iconKind, 300); if (error is not null) return BadRequest(new { message = error }); if (id == parentSubCategoryId) return BadRequest(new { message = "لا يمكن أن يكون الزر أبًا لنفسه." });
        var category = await context.ScriptCategories.FirstOrDefaultAsync(item => item.Id == categoryId && !item.IsDeleted, ct); if (category is null) return BadRequest(new { message = "التصنيف الأب غير موجود." });
        if (parentSubCategoryId.HasValue && !await context.ScriptSubCategories.AnyAsync(item => item.Id == parentSubCategoryId && item.ScriptCategoryId == categoryId && !item.IsDeleted, ct)) return BadRequest(new { message = "الزر الأب غير موجود." });
        var script = await context.StoreScripts.FindAsync([category.ScriptDefinitionId], ct); if (script is null) return BadRequest(new { message = "تعريف السكربت غير موجود." });
        var item = id > 0 ? await context.ScriptSubCategories.FirstOrDefaultAsync(row => row.Id == id && row.ScriptCategoryId == categoryId, ct) : null; if (id > 0 && item is null) return BadRequest(new { message = "الزر الفرعي غير موجود." });
        if (item is null) { item = new ScriptSubCategory { ScriptCategoryId = categoryId, CreatedAt = IstanbulTimeHelper.Now, IsEnabled = true, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name }; context.ScriptSubCategories.Add(item); }
        item.ParentSubCategoryId = parentSubCategoryId; item.Key = key.Trim(); item.Label = label.Trim(); item.Icon = icon.Trim(); item.IconKind = iconKind.Trim().ToLowerInvariant(); item.ColorToken = Clean(colorToken); item.IsCountryScoped = isCountryScoped; item.SortOrder = sortOrder; Touch(item); Bump(script); await context.SaveChangesAsync(ct);
        return Ok(new { success = true, id = item.Id, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SaveMessagesAjax")]
    public async Task<IActionResult> SaveMessagesAjax([FromForm] int subCategoryId, [FromForm] string messagesJson, CancellationToken ct)
    {
        var node = await context.ScriptSubCategories.Include(item => item.ScriptCategory).FirstOrDefaultAsync(item => item.Id == subCategoryId, ct); if (node?.ScriptCategory is null) return BadRequest(new { message = "الزر الفرعي غير موجود." });
        List<MessageInput>? input; try { input = JsonSerializer.Deserialize<List<MessageInput>>(messagesJson ?? "[]", JsonOptions); } catch (JsonException) { return BadRequest(new { message = "تعذر قراءة قائمة الرسائل." }); } input ??= [];
        if (input.Any(item => string.IsNullOrWhiteSpace(item.Text))) return BadRequest(new { message = "نص الرسالة مطلوب لكل خطوة." });
        var countries = await context.ScriptCountries.Where(item => item.ScriptDefinitionId == node.ScriptCategory.ScriptDefinitionId && !item.IsDeleted).ToDictionaryAsync(item => item.Code, item => item.Id, StringComparer.OrdinalIgnoreCase, ct);
        if (input.Any(item => item.CountryCode != null && !countries.ContainsKey(item.CountryCode))) return BadRequest(new { message = "رمز دولة غير معروف لهذا السكربت." });
        await context.ScriptMessages.Where(item => item.ScriptSubCategoryId == subCategoryId).ExecuteDeleteAsync(ct);
        context.ScriptMessages.AddRange(input.Select(item => new ScriptMessage { ScriptSubCategoryId = subCategoryId, ScriptCountryId = item.CountryCode is null ? null : countries[item.CountryCode], Phase = item.Phase, StepOrder = item.StepOrder, Gender = NormalizeGender(item.Gender), Text = item.Text.Trim() }));
        var script = await context.StoreScripts.FindAsync([node.ScriptCategory.ScriptDefinitionId], ct); Bump(script!); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script!.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SaveThemeAjax")]
    public async Task<IActionResult> SaveThemeAjax([FromForm] int folderId, [FromForm] string tokensJson, CancellationToken ct)
    {
        var script = await ByFolder(folderId, ct); if (script is null) return BadRequest(new { message = "تعريف السكربت غير موجود." }); var input = ParseList<KeyValueOrder>(tokensJson); if (input is null) return BadRequest(new { message = "تعذر قراءة قائمة الألوان." });
        if (!ValidPairs(input, 64, 64)) return BadRequest(new { message = "مفتاح أو قيمة اللون غير صحيحة أو مكررة." }); await context.ScriptThemeTokens.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); context.ScriptThemeTokens.AddRange(input.Select(item => new ScriptThemeToken { ScriptDefinitionId = script.Id, Key = item.Key.Trim(), Value = item.Value.Trim(), SortOrder = item.SortOrder })); Bump(script); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SaveCountryAjax")]
    public async Task<IActionResult> SaveCountryAjax([FromForm] int folderId, [FromForm] int id, [FromForm] string code, [FromForm] string label, [FromForm] string flagHex, [FromForm] int sortOrder, [FromForm] bool isEnabled, [FromForm] string? valuesJson, CancellationToken ct)
    {
        var script = await ByFolder(folderId, ct); if (script is null) return BadRequest(new { message = "تعريف السكربت غير موجود." }); code = code.Trim().ToUpperInvariant(); if (code.Length is 0 or > 8 || string.IsNullOrWhiteSpace(label) || label.Length > 120 || string.IsNullOrWhiteSpace(flagHex) || flagHex.Length > 32) return BadRequest(new { message = "بيانات الدولة غير صحيحة." });
        var values = ParseList<KeyValueOrder>(valuesJson); if (values is null || !ValidPairs(values, 64, int.MaxValue)) return BadRequest(new { message = "قيم الدولة غير صحيحة أو مكررة." }); if (await context.ScriptCountries.AnyAsync(item => item.ScriptDefinitionId == script.Id && item.Id != id && !item.IsDeleted && item.Code == code, ct)) return Conflict(new { message = "رمز الدولة مستخدم بالفعل." });
        var country = id > 0 ? await context.ScriptCountries.FirstOrDefaultAsync(item => item.Id == id && item.ScriptDefinitionId == script.Id, ct) : null; if (id > 0 && country is null) return NotFound(); if (country is null) { country = new ScriptCountry { ScriptDefinitionId = script.Id }; context.ScriptCountries.Add(country); await context.SaveChangesAsync(ct); }
        country.Code = code; country.Label = label.Trim(); country.FlagHex = flagHex.Trim(); country.SortOrder = sortOrder; country.IsEnabled = isEnabled; await context.ScriptCountryValues.Where(item => item.ScriptCountryId == country.Id).ExecuteDeleteAsync(ct); context.ScriptCountryValues.AddRange(values.Select(item => new ScriptCountryValue { ScriptCountryId = country.Id, Key = item.Key.Trim(), Value = item.Value })); Bump(script); await context.SaveChangesAsync(ct); return Ok(new { success = true, id = country.Id, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SaveTargetsAjax")]
    public async Task<IActionResult> SaveTargetsAjax([FromForm] int folderId, [FromForm] string targetsJson, CancellationToken ct)
    {
        var script = await ByFolder(folderId, ct); if (script is null) return BadRequest(new { message = "تعريف السكربت غير موجود." }); var input = ParseList<TargetInput>(targetsJson); if (input is null || input.Any(item => !TargetKinds.Contains(item.Kind) || string.IsNullOrWhiteSpace(item.Value) || item.Value.Length > 64)) return BadRequest(new { message = "قائمة المعرفات غير صحيحة." });
        var pairs = input.Select(item => item.Kind + "\0" + item.Value.Trim()).ToArray(); if (pairs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != pairs.Length) return BadRequest(new { message = "يوجد معرف مكرر." });
        foreach (var item in input) if (await context.ScriptTargets.AnyAsync(row => row.ScriptDefinitionId != script.Id && !row.IsDeleted && row.Kind == item.Kind && row.Value == item.Value.Trim(), ct)) return Conflict(new { message = "أحد المعرفات مستخدم بالفعل من متجر آخر." });
        await context.ScriptTargets.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); context.ScriptTargets.AddRange(input.Select(item => new ScriptTarget { ScriptDefinitionId = script.Id, Kind = item.Kind, Value = item.Value.Trim(), SortOrder = item.SortOrder })); Bump(script); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/ToggleEnabledAjax")]
    public async Task<IActionResult> ToggleEnabledAjax([FromForm] string entityType, [FromForm] int id, [FromForm] bool isEnabled, CancellationToken ct)
    {
        StoreScript? script = null;
        if (entityType == "Category") { var item = await context.ScriptCategories.FindAsync([id], ct); if (item is null) return NotFound(); item.IsEnabled = isEnabled; Touch(item); script = await context.StoreScripts.FindAsync([item.ScriptDefinitionId], ct); }
        else if (entityType == "SubCategory") { var item = await context.ScriptSubCategories.Include(row => row.ScriptCategory).FirstOrDefaultAsync(row => row.Id == id, ct); if (item?.ScriptCategory is null) return NotFound(); item.IsEnabled = isEnabled; Touch(item); script = await context.StoreScripts.FindAsync([item.ScriptCategory.ScriptDefinitionId], ct); }
        else if (entityType == "Country") { var item = await context.ScriptCountries.FindAsync([id], ct); if (item is null) return NotFound(); item.IsEnabled = isEnabled; script = await context.StoreScripts.FindAsync([item.ScriptDefinitionId], ct); }
        else return BadRequest(new { message = "نوع العنصر غير معروف." }); Bump(script!); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script!.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/ReorderAjax")]
    public async Task<IActionResult> ReorderAjax([FromForm] string entityType, [FromForm] int id, [FromForm] int otherId, CancellationToken ct)
    {
        StoreScript? script;
        if (entityType == "Category") { var rows = await context.ScriptCategories.Where(item => item.Id == id || item.Id == otherId).ToListAsync(ct); if (rows.Count != 2 || rows[0].ScriptDefinitionId != rows[1].ScriptDefinitionId) return BadRequest(); (rows[0].SortOrder, rows[1].SortOrder) = (rows[1].SortOrder, rows[0].SortOrder); script = await context.StoreScripts.FindAsync([rows[0].ScriptDefinitionId], ct); }
        else if (entityType == "SubCategory") { var rows = await context.ScriptSubCategories.Include(item => item.ScriptCategory).Where(item => item.Id == id || item.Id == otherId).ToListAsync(ct); if (rows.Count != 2 || rows[0].ScriptCategoryId != rows[1].ScriptCategoryId || rows[0].ParentSubCategoryId != rows[1].ParentSubCategoryId) return BadRequest(); (rows[0].SortOrder, rows[1].SortOrder) = (rows[1].SortOrder, rows[0].SortOrder); script = await context.StoreScripts.FindAsync([rows[0].ScriptCategory!.ScriptDefinitionId], ct); }
        else return BadRequest(new { message = "نوع العنصر غير معروف." }); Bump(script!); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script!.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SoftDeleteAjax")]
    public Task<IActionResult> SoftDeleteAjax([FromForm] string entityType, [FromForm] int id, CancellationToken ct) => SetDeleted(entityType, id, true, ct);

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/RestoreAjax")]
    public Task<IActionResult> RestoreAjax([FromForm] string entityType, [FromForm] int id, CancellationToken ct) => SetDeleted(entityType, id, false, ct);

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/ClearScriptDataAjax")]
    public async Task<IActionResult> ClearScriptDataAjax([FromForm] int folderId, CancellationToken ct)
    {
        var script = await ByFolder(folderId, ct); if (script is null) return NotFound(); await using var tx = await context.Database.BeginTransactionAsync(ct);
        await context.ScriptMessages.Where(item => item.ScriptCategory!.ScriptDefinitionId == script.Id || item.ScriptSubCategory!.ScriptCategory!.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct);
        await context.ScriptSubCategories.Where(item => item.ScriptCategory!.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); await context.ScriptCategories.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); await context.ScriptCountryValues.Where(item => item.ScriptCountry!.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); await context.ScriptCountries.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); await context.ScriptTargets.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); await context.ScriptThemeTokens.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); await context.ScriptSettings.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); await context.ScriptTranslations.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); Bump(script); await context.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(new { success = true, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpGet("/StoreScript/GetScriptHistoryAjax")]
    public async Task<IActionResult> GetScriptHistoryAjax(int folderId, CancellationToken ct) { var id = await context.StoreScripts.Where(item => item.StoreCodeFolderId == folderId).Select(item => (int?)item.Id).FirstOrDefaultAsync(ct); if (!id.HasValue) return Ok(new { success = true, history = Array.Empty<object>() }); return Ok(new { success = true, history = await context.ScriptEditHistories.AsNoTracking().Where(item => item.ScriptDefinitionId == id).OrderByDescending(item => item.CreatedAt).Take(500).ToListAsync(ct) }); }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/RestoreHistoryEntryAjax")]
    public async Task<IActionResult> RestoreHistoryEntryAjax([FromForm] int id, CancellationToken ct) { var history = await context.ScriptEditHistories.FindAsync([id], ct); if (history is null) return NotFound(); History(history.ScriptDefinitionId, history.EntityType, history.EntityId, history.Field, history.NewValue, history.OldValue, true); var script = await context.StoreScripts.FindAsync([history.ScriptDefinitionId], ct); Bump(script!); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script!.RevisionStamp, restoredValue = history.OldValue }); }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SetActiveAjax")]
    public async Task<IActionResult> SetActiveAjax([FromForm] int id, [FromForm] bool? isActive, CancellationToken ct) { var script = await context.StoreScripts.FindAsync([id], ct); if (script is null || script.IsDeleted) return NotFound(); var activate = isActive ?? !script.IsActive; if (activate) await context.StoreScripts.Where(item => item.Id != id && item.ManufacturingCompanyId == script.ManufacturingCompanyId && item.Platform == script.Platform && item.IsActive && !item.IsDeleted).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, false), ct); script.IsActive = activate; Bump(script); await context.SaveChangesAsync(ct); return Ok(new { success = true, script.IsActive, revision = script.RevisionStamp }); }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SaveSequenceAjax")]
    public async Task<IActionResult> SaveSequenceAjax([FromForm] int subCategoryId, [FromForm] string? nodeType, [FromForm] int? categoryId, [FromForm] string? maleText, [FromForm] string? femaleText, CancellationToken ct)
    {
        int definitionId; if (string.Equals(nodeType, "Category", StringComparison.OrdinalIgnoreCase) && categoryId.HasValue) { var category = await context.ScriptCategories.FindAsync([categoryId.Value], ct); if (category is null) return NotFound(); definitionId = category.ScriptDefinitionId; await context.ScriptMessages.Where(item => item.ScriptCategoryId == categoryId).ExecuteDeleteAsync(ct); AddSequence(categoryId, null, maleText, femaleText); } else { var node = await context.ScriptSubCategories.Include(item => item.ScriptCategory).FirstOrDefaultAsync(item => item.Id == subCategoryId, ct); if (node?.ScriptCategory is null) return NotFound(); definitionId = node.ScriptCategory.ScriptDefinitionId; await context.ScriptMessages.Where(item => item.ScriptSubCategoryId == subCategoryId).ExecuteDeleteAsync(ct); AddSequence(null, subCategoryId, maleText, femaleText); } var script = await context.StoreScripts.FindAsync([definitionId], ct); Bump(script!); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script!.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/ImportSeedAjax")]
    public async Task<IActionResult> ImportSeedAjax([FromForm] int folderId, [FromForm] string? seedJson, CancellationToken ct) { var script = await ByFolder(folderId, ct); if (script is null) return NotFound(); if (string.IsNullOrWhiteSpace(seedJson)) return BadRequest(new { message = "بيانات الاستيراد مطلوبة." }); try { using var document = JsonDocument.Parse(seedJson); script.Notes = document.RootElement.TryGetProperty("notes", out var notes) ? notes.GetString() : script.Notes; } catch (JsonException) { return BadRequest(new { message = "تعذر قراءة ملف الاستيراد." }); } Bump(script); History(script.Id, "ScriptDefinition", script.Id, "ImportSeed", null, "Imported"); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script.RevisionStamp }); }

    private async Task<StoreScript?> LoadTree(int id, bool liveOnly, CancellationToken ct) => await context.StoreScripts.AsSplitQuery().Include(item => item.Targets).Include(item => item.ThemeTokens).Include(item => item.Settings).Include(item => item.Countries).ThenInclude(item => item.Values).Include(item => item.Categories).ThenInclude(item => item.Messages).Include(item => item.Categories).ThenInclude(item => item.SubCategories).ThenInclude(item => item.Messages).Include(item => item.Translations).FirstOrDefaultAsync(item => item.Id == id && (!liveOnly || item.IsActive && !item.IsDeleted), ct);
    private object Tree(StoreScript script, bool liveOnly) => new { script.Id, script.StoreCodeFolderId, script.ManufacturingCompanyId, script.Platform, script.EngineVersion, revision = script.RevisionStamp, script.IsActive, targets = script.Targets.Where(item => !liveOnly || !item.IsDeleted).OrderBy(item => item.SortOrder), theme = script.ThemeTokens.OrderBy(item => item.SortOrder), settings = script.Settings.OrderBy(item => item.SortOrder), countries = script.Countries.Where(item => !liveOnly || item.IsEnabled && !item.IsDeleted).OrderBy(item => item.SortOrder).Select(item => new { item.Id, item.Code, item.Label, item.FlagHex, item.IsEnabled, values = item.Values.ToDictionary(value => value.Key, value => value.Value) }), categories = script.Categories.Where(item => !liveOnly || item.IsEnabled && !item.IsDeleted).OrderBy(item => item.SortOrder).Select(item => new { item.Id, item.Key, item.Label, item.Icon, item.IconKind, item.SortOrder, item.IsEnabled, messages = item.Messages.OrderBy(message => message.Phase).ThenBy(message => message.StepOrder), subCategories = item.SubCategories.Where(node => !liveOnly || node.IsEnabled && !node.IsDeleted).OrderBy(node => node.SortOrder).Select(node => new { node.Id, node.ParentSubCategoryId, node.Key, node.Label, node.Icon, node.IconKind, node.ColorToken, node.IsCountryScoped, node.SortOrder, node.IsEnabled, messages = node.Messages.OrderBy(message => message.Phase).ThenBy(message => message.StepOrder) }) }), translations = script.Translations.Where(item => !liveOnly || !item.IsDeleted) };
    private async Task<StoreScript?> ByFolder(int id, CancellationToken ct) => await context.StoreScripts.FirstOrDefaultAsync(item => item.StoreCodeFolderId == id && !item.IsDeleted, ct);
    private bool CanManage() => User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector");
    private async Task<bool> CanAccess(int companyId, CancellationToken ct) => CanManage() || await context.EmployeeManufacturingCompanies.AnyAsync(item => item.ApplicationUserId == (User.GetUserId() ?? "") && item.ManufacturingCompanyId == companyId && item.CanSeeManufacturingCompany, ct);
    private async Task<IActionResult> ScriptFile(string name, CancellationToken ct) { var path = Path.Combine(environment.ContentRootPath, "ScriptAssets", name); if (!System.IO.File.Exists(path)) return StatusCode(503, "script asset is not deployed yet"); return Content(await System.IO.File.ReadAllTextAsync(path, ct), "application/javascript"); }
    private static string? ValidateNode(string key, string label, string icon, string iconKind, int maxLabel) { if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > 64) return "المفتاح غير صحيح."; if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > maxLabel) return "العنوان غير صحيح."; if (string.IsNullOrWhiteSpace(icon) || icon.Trim().Length > 64) return "الأيقونة غير صحيحة."; return IconKinds.Contains(iconKind.Trim()) ? null : "نوع الأيقونة غير صحيح."; }
    private static List<T>? ParseList<T>(string? json) { try { return JsonSerializer.Deserialize<List<T>>(json ?? "[]", JsonOptions) ?? []; } catch (JsonException) { return null; } }
    private static bool ValidPairs(List<KeyValueOrder> items, int keyMax, int valueMax) => items.All(item => !string.IsNullOrWhiteSpace(item.Key) && item.Key.Trim().Length <= keyMax && !string.IsNullOrEmpty(item.Value) && item.Value.Length <= valueMax) && items.Select(item => item.Key.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == items.Count;
    private async Task<IActionResult> SetDeleted(string type, int id, bool deleted, CancellationToken ct) { StoreScript? script; if (type == "Category") { var item = await context.ScriptCategories.FindAsync([id], ct); if (item is null) return NotFound(); item.IsDeleted = deleted; item.DeletedAt = deleted ? IstanbulTimeHelper.Now : null; script = await context.StoreScripts.FindAsync([item.ScriptDefinitionId], ct); } else if (type == "SubCategory") { var item = await context.ScriptSubCategories.Include(row => row.ScriptCategory).FirstOrDefaultAsync(row => row.Id == id, ct); if (item?.ScriptCategory is null) return NotFound(); item.IsDeleted = deleted; item.DeletedAt = deleted ? IstanbulTimeHelper.Now : null; script = await context.StoreScripts.FindAsync([item.ScriptCategory.ScriptDefinitionId], ct); } else if (type == "Country") { var item = await context.ScriptCountries.FindAsync([id], ct); if (item is null) return NotFound(); item.IsDeleted = deleted; script = await context.StoreScripts.FindAsync([item.ScriptDefinitionId], ct); } else return BadRequest(new { message = "نوع العنصر غير معروف." }); Bump(script!); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script!.RevisionStamp }); }
    private void History(int scriptId, string entity, int entityId, string field, string? oldValue, string? newValue, bool restore = false) => context.ScriptEditHistories.Add(new ScriptEditHistory { ScriptDefinitionId = scriptId, EntityType = entity, EntityId = entityId, Field = field, OldValue = oldValue, NewValue = newValue, IsRestoreAction = restore, CreatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name });
    private void Bump(StoreScript script) { script.RevisionStamp = Math.Max(script.RevisionStamp + 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); script.UpdatedAt = IstanbulTimeHelper.Now; script.UpdatedByUserId = User.GetUserId(); script.UpdatedByName = User.Identity?.Name; }
    private void Touch(ScriptCategory item) { item.UpdatedAt = IstanbulTimeHelper.Now; item.UpdatedByUserId = User.GetUserId(); item.UpdatedByName = User.Identity?.Name; }
    private void Touch(ScriptSubCategory item) { item.UpdatedAt = IstanbulTimeHelper.Now; item.UpdatedByUserId = User.GetUserId(); item.UpdatedByName = User.Identity?.Name; }
    private void AddSequence(int? categoryId, int? subCategoryId, string? male, string? female) { var step = 0; foreach (var (gender, text) in new[] { ("M", male), ("F", female) }) foreach (var line in (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) context.ScriptMessages.Add(new ScriptMessage { ScriptCategoryId = categoryId, ScriptSubCategoryId = subCategoryId, Phase = 0, StepOrder = step++, Gender = gender, Text = line }); }
    private static string NormalizeGender(string? value) => value?.Trim().ToUpperInvariant() is "F" ? "F" : "M";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record MessageInput(string? CountryCode, int Phase, int StepOrder, string? Gender, string Text);
public sealed record KeyValueOrder(string Key, string Value, int SortOrder);
public sealed record TargetInput(string Kind, string Value, int SortOrder);
