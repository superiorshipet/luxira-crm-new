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
        var before = item is null ? null : new { item.Key, item.Label, item.Icon, item.IconKind, item.SortOrder };
        if (item is null) { item = new ScriptCategory { ScriptDefinitionId = script.Id, CreatedAt = IstanbulTimeHelper.Now, IsEnabled = true, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name }; context.ScriptCategories.Add(item); }
        item.Key = key.Trim(); item.Label = label.Trim(); item.Icon = icon.Trim(); item.IconKind = iconKind.Trim().ToLowerInvariant(); item.SortOrder = sortOrder; Touch(item); Bump(script); await context.SaveChangesAsync(ct);
        if (before is null) History(script.Id, "ScriptCategory", item.Id, "Created", null, item.Label); else { Track(script.Id, "ScriptCategory", item.Id, "Key", before.Key, item.Key); Track(script.Id, "ScriptCategory", item.Id, "Label", before.Label, item.Label); Track(script.Id, "ScriptCategory", item.Id, "Icon", before.Icon, item.Icon); Track(script.Id, "ScriptCategory", item.Id, "IconKind", before.IconKind, item.IconKind); Track(script.Id, "ScriptCategory", item.Id, "SortOrder", before.SortOrder.ToString(), item.SortOrder.ToString()); } await context.SaveChangesAsync(ct);
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
        var before = item is null ? null : new { item.ParentSubCategoryId, item.Key, item.Label, item.Icon, item.IconKind, item.ColorToken, item.IsCountryScoped, item.SortOrder };
        if (item is null) { item = new ScriptSubCategory { ScriptCategoryId = categoryId, CreatedAt = IstanbulTimeHelper.Now, IsEnabled = true, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name }; context.ScriptSubCategories.Add(item); }
        item.ParentSubCategoryId = parentSubCategoryId; item.Key = key.Trim(); item.Label = label.Trim(); item.Icon = icon.Trim(); item.IconKind = iconKind.Trim().ToLowerInvariant(); item.ColorToken = Clean(colorToken); item.IsCountryScoped = isCountryScoped; item.SortOrder = sortOrder; Touch(item); Bump(script); await context.SaveChangesAsync(ct);
        if (before is null) History(script.Id, "ScriptSubCategory", item.Id, "Created", null, item.Label); else { Track(script.Id, "ScriptSubCategory", item.Id, "ParentSubCategoryId", before.ParentSubCategoryId?.ToString(), item.ParentSubCategoryId?.ToString()); Track(script.Id, "ScriptSubCategory", item.Id, "Key", before.Key, item.Key); Track(script.Id, "ScriptSubCategory", item.Id, "Label", before.Label, item.Label); Track(script.Id, "ScriptSubCategory", item.Id, "Icon", before.Icon, item.Icon); Track(script.Id, "ScriptSubCategory", item.Id, "IconKind", before.IconKind, item.IconKind); Track(script.Id, "ScriptSubCategory", item.Id, "ColorToken", before.ColorToken, item.ColorToken); Track(script.Id, "ScriptSubCategory", item.Id, "IsCountryScoped", before.IsCountryScoped.ToString(), item.IsCountryScoped.ToString()); Track(script.Id, "ScriptSubCategory", item.Id, "SortOrder", before.SortOrder.ToString(), item.SortOrder.ToString()); } await context.SaveChangesAsync(ct);
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
        var oldMessages = await context.ScriptMessages.AsNoTracking().Where(item => item.ScriptSubCategoryId == subCategoryId).OrderBy(item => item.StepOrder).Select(item => new MessageInput(item.ScriptCountry!.Code, item.Phase, item.StepOrder, item.Gender, item.Text)).ToListAsync(ct);
        await context.ScriptMessages.Where(item => item.ScriptSubCategoryId == subCategoryId).ExecuteDeleteAsync(ct);
        context.ScriptMessages.AddRange(input.Select(item => new ScriptMessage { ScriptSubCategoryId = subCategoryId, ScriptCountryId = item.CountryCode is null ? null : countries[item.CountryCode], Phase = item.Phase, StepOrder = item.StepOrder, Gender = NormalizeGender(item.Gender), Text = item.Text.Trim() }));
        var script = await context.StoreScripts.FindAsync([node.ScriptCategory.ScriptDefinitionId], ct); Track(script!.Id, "ScriptSubCategory", node.Id, "Messages", JsonSerializer.Serialize(oldMessages), JsonSerializer.Serialize(input)); Bump(script); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SaveThemeAjax")]
    public async Task<IActionResult> SaveThemeAjax([FromForm] int folderId, [FromForm] string tokensJson, CancellationToken ct)
    {
        var script = await ByFolder(folderId, ct); if (script is null) return BadRequest(new { message = "تعريف السكربت غير موجود." }); var input = ParseList<KeyValueOrder>(tokensJson); if (input is null) return BadRequest(new { message = "تعذر قراءة قائمة الألوان." });
        if (!ValidPairs(input, 64, 64)) return BadRequest(new { message = "مفتاح أو قيمة اللون غير صحيحة أو مكررة." }); var old = await context.ScriptThemeTokens.AsNoTracking().Where(item => item.ScriptDefinitionId == script.Id).OrderBy(item => item.SortOrder).Select(item => new KeyValueOrder(item.Key, item.Value, item.SortOrder)).ToListAsync(ct); await context.ScriptThemeTokens.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); context.ScriptThemeTokens.AddRange(input.Select(item => new ScriptThemeToken { ScriptDefinitionId = script.Id, Key = item.Key.Trim(), Value = item.Value.Trim(), SortOrder = item.SortOrder })); Track(script.Id, "ScriptDefinition", script.Id, "Theme", JsonSerializer.Serialize(old), JsonSerializer.Serialize(input)); Bump(script); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SaveCountryAjax")]
    public async Task<IActionResult> SaveCountryAjax([FromForm] int folderId, [FromForm] int id, [FromForm] string code, [FromForm] string label, [FromForm] string flagHex, [FromForm] int sortOrder, [FromForm] bool isEnabled, [FromForm] string? valuesJson, CancellationToken ct)
    {
        var script = await ByFolder(folderId, ct); if (script is null) return BadRequest(new { message = "تعريف السكربت غير موجود." }); code = code.Trim().ToUpperInvariant(); if (code.Length is 0 or > 8 || string.IsNullOrWhiteSpace(label) || label.Length > 120 || string.IsNullOrWhiteSpace(flagHex) || flagHex.Length > 32) return BadRequest(new { message = "بيانات الدولة غير صحيحة." });
        var values = ParseList<KeyValueOrder>(valuesJson); if (values is null || !ValidPairs(values, 64, int.MaxValue)) return BadRequest(new { message = "قيم الدولة غير صحيحة أو مكررة." }); if (await context.ScriptCountries.AnyAsync(item => item.ScriptDefinitionId == script.Id && item.Id != id && !item.IsDeleted && item.Code == code, ct)) return Conflict(new { message = "رمز الدولة مستخدم بالفعل." });
        var country = id > 0 ? await context.ScriptCountries.Include(item => item.Values).FirstOrDefaultAsync(item => item.Id == id && item.ScriptDefinitionId == script.Id, ct) : null; if (id > 0 && country is null) return NotFound();
        var isNew = country is null;
        if (country is null) { country = new ScriptCountry { ScriptDefinitionId = script.Id }; context.ScriptCountries.Add(country); await context.SaveChangesAsync(ct); }
        var before = new { country.Code, country.Label, country.FlagHex, country.SortOrder, country.IsEnabled, Values = country.Values.OrderBy(item => item.Id).Select((item, index) => new KeyValueOrder(item.Key, item.Value, index)).ToList() };
        country.Code = code; country.Label = label.Trim(); country.FlagHex = flagHex.Trim(); country.SortOrder = sortOrder; country.IsEnabled = isEnabled; await context.ScriptCountryValues.Where(item => item.ScriptCountryId == country.Id).ExecuteDeleteAsync(ct); context.ScriptCountryValues.AddRange(values.Select(item => new ScriptCountryValue { ScriptCountryId = country.Id, Key = item.Key.Trim(), Value = item.Value }));
        if (isNew) { History(script.Id, "ScriptCountry", country.Id, "Created", null, $"{country.Code} - {country.Label}"); Track(script.Id, "ScriptCountry", country.Id, "Values", "[]", JsonSerializer.Serialize(values)); }
        else { Track(script.Id, "ScriptCountry", country.Id, "Code", before.Code, country.Code); Track(script.Id, "ScriptCountry", country.Id, "Label", before.Label, country.Label); Track(script.Id, "ScriptCountry", country.Id, "FlagHex", before.FlagHex, country.FlagHex); Track(script.Id, "ScriptCountry", country.Id, "SortOrder", before.SortOrder.ToString(), country.SortOrder.ToString()); Track(script.Id, "ScriptCountry", country.Id, "IsEnabled", before.IsEnabled.ToString(), country.IsEnabled.ToString()); Track(script.Id, "ScriptCountry", country.Id, "Values", JsonSerializer.Serialize(before.Values), JsonSerializer.Serialize(values)); }
        Bump(script); await context.SaveChangesAsync(ct); return Ok(new { success = true, id = country.Id, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SaveTargetsAjax")]
    public async Task<IActionResult> SaveTargetsAjax([FromForm] int folderId, [FromForm] string targetsJson, CancellationToken ct)
    {
        var script = await ByFolder(folderId, ct); if (script is null) return BadRequest(new { message = "تعريف السكربت غير موجود." }); var input = ParseList<TargetInput>(targetsJson); if (input is null || input.Any(item => !TargetKinds.Contains(item.Kind) || string.IsNullOrWhiteSpace(item.Value) || item.Value.Length > 64)) return BadRequest(new { message = "قائمة المعرفات غير صحيحة." });
        var pairs = input.Select(item => item.Kind + "\0" + item.Value.Trim()).ToArray(); if (pairs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != pairs.Length) return BadRequest(new { message = "يوجد معرف مكرر." });
        foreach (var item in input) if (await context.ScriptTargets.AnyAsync(row => row.ScriptDefinitionId != script.Id && !row.IsDeleted && row.Kind == item.Kind && row.Value == item.Value.Trim(), ct)) return Conflict(new { message = "أحد المعرفات مستخدم بالفعل من متجر آخر." });
        var old = await context.ScriptTargets.AsNoTracking().Where(item => item.ScriptDefinitionId == script.Id).OrderBy(item => item.SortOrder).Select(item => new TargetInput(item.Kind, item.Value, item.SortOrder)).ToListAsync(ct); await context.ScriptTargets.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); context.ScriptTargets.AddRange(input.Select(item => new ScriptTarget { ScriptDefinitionId = script.Id, Kind = item.Kind, Value = item.Value.Trim(), SortOrder = item.SortOrder })); Track(script.Id, "ScriptDefinition", script.Id, "Targets", JsonSerializer.Serialize(old), JsonSerializer.Serialize(input)); Bump(script); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/ToggleEnabledAjax")]
    public async Task<IActionResult> ToggleEnabledAjax([FromForm] string entityType, [FromForm] int id, [FromForm] bool isEnabled, CancellationToken ct)
    {
        StoreScript? script = null;
        if (entityType == "Category") { var item = await context.ScriptCategories.FindAsync([id], ct); if (item is null) return NotFound(); script = await context.StoreScripts.FindAsync([item.ScriptDefinitionId], ct); Track(script!.Id, "ScriptCategory", item.Id, "IsEnabled", item.IsEnabled.ToString(), isEnabled.ToString()); item.IsEnabled = isEnabled; Touch(item); }
        else if (entityType == "SubCategory") { var item = await context.ScriptSubCategories.Include(row => row.ScriptCategory).FirstOrDefaultAsync(row => row.Id == id, ct); if (item?.ScriptCategory is null) return NotFound(); script = await context.StoreScripts.FindAsync([item.ScriptCategory.ScriptDefinitionId], ct); Track(script!.Id, "ScriptSubCategory", item.Id, "IsEnabled", item.IsEnabled.ToString(), isEnabled.ToString()); item.IsEnabled = isEnabled; Touch(item); }
        else if (entityType == "Country") { var item = await context.ScriptCountries.FindAsync([id], ct); if (item is null) return NotFound(); script = await context.StoreScripts.FindAsync([item.ScriptDefinitionId], ct); Track(script!.Id, "ScriptCountry", item.Id, "IsEnabled", item.IsEnabled.ToString(), isEnabled.ToString()); item.IsEnabled = isEnabled; }
        else return BadRequest(new { message = "نوع العنصر غير معروف." }); Bump(script!); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script!.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/ReorderAjax")]
    public async Task<IActionResult> ReorderAjax([FromForm] string entityType, [FromForm] int id, [FromForm] int otherId, CancellationToken ct)
    {
        StoreScript? script;
        if (entityType == "Category") { var rows = await context.ScriptCategories.Where(item => item.Id == id || item.Id == otherId).ToListAsync(ct); if (rows.Count != 2 || rows[0].ScriptDefinitionId != rows[1].ScriptDefinitionId) return BadRequest(); script = await context.StoreScripts.FindAsync([rows[0].ScriptDefinitionId], ct); (rows[0].SortOrder, rows[1].SortOrder) = (rows[1].SortOrder, rows[0].SortOrder); }
        else if (entityType == "SubCategory") { var rows = await context.ScriptSubCategories.Include(item => item.ScriptCategory).Where(item => item.Id == id || item.Id == otherId).ToListAsync(ct); if (rows.Count != 2 || rows[0].ScriptCategoryId != rows[1].ScriptCategoryId || rows[0].ParentSubCategoryId != rows[1].ParentSubCategoryId) return BadRequest(); script = await context.StoreScripts.FindAsync([rows[0].ScriptCategory!.ScriptDefinitionId], ct); (rows[0].SortOrder, rows[1].SortOrder) = (rows[1].SortOrder, rows[0].SortOrder); }
        else return BadRequest(new { message = "نوع العنصر غير معروف." }); Bump(script!); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script!.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SoftDeleteAjax")]
    public Task<IActionResult> SoftDeleteAjax([FromForm] string entityType, [FromForm] int id, CancellationToken ct) => SetDeleted(entityType, id, true, ct);

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/RestoreAjax")]
    public Task<IActionResult> RestoreAjax([FromForm] string entityType, [FromForm] int id, CancellationToken ct) => SetDeleted(entityType, id, false, ct);

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpGet("/StoreScript/ClearScriptDataAjax")]
    [HttpPost("/StoreScript/ClearScriptDataAjax")]
    public async Task<IActionResult> ClearScriptDataAjax([FromForm] int folderId, CancellationToken ct)
    {
        var scripts = await context.StoreScripts.Where(item => item.StoreCodeFolderId == folderId).ToListAsync(ct); if (scripts.Count == 0) return BadRequest(new { message = "لا يوجد سكربت لهذا الملف بعد." });
        var scriptIds = scripts.Select(item => item.Id).ToArray();
        var categoryIds = await context.ScriptCategories.Where(item => scriptIds.Contains(item.ScriptDefinitionId)).Select(item => item.Id).ToArrayAsync(ct);
        var subCategories = await context.ScriptSubCategories.Where(item => categoryIds.Contains(item.ScriptCategoryId)).ToListAsync(ct); var subIds = subCategories.Select(item => item.Id).ToArray();
        var countryIds = await context.ScriptCountries.Where(item => scriptIds.Contains(item.ScriptDefinitionId)).Select(item => item.Id).ToArrayAsync(ct);
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        await context.ScriptMessages.Where(item => item.ScriptCategoryId != null && categoryIds.Contains(item.ScriptCategoryId.Value) || item.ScriptSubCategoryId != null && subIds.Contains(item.ScriptSubCategoryId.Value) || item.ScriptCountryId != null && countryIds.Contains(item.ScriptCountryId.Value)).ExecuteDeleteAsync(ct);
        await context.ScriptCountryValues.Where(item => countryIds.Contains(item.ScriptCountryId)).ExecuteDeleteAsync(ct);
        var byId = subCategories.ToDictionary(item => item.Id); int Depth(ScriptSubCategory item) { var depth = 0; var parent = item.ParentSubCategoryId; while (parent.HasValue && byId.TryGetValue(parent.Value, out var node) && depth++ < 64) parent = node.ParentSubCategoryId; return depth; }
        foreach (var level in subCategories.GroupBy(Depth).OrderByDescending(group => group.Key)) { var ids = level.Select(item => item.Id).ToArray(); await context.ScriptSubCategories.Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync(ct); }
        await context.ScriptCategories.Where(item => categoryIds.Contains(item.Id)).ExecuteDeleteAsync(ct); await context.ScriptCountries.Where(item => countryIds.Contains(item.Id)).ExecuteDeleteAsync(ct); await context.ScriptTargets.Where(item => scriptIds.Contains(item.ScriptDefinitionId)).ExecuteDeleteAsync(ct); await context.ScriptThemeTokens.Where(item => scriptIds.Contains(item.ScriptDefinitionId)).ExecuteDeleteAsync(ct); await context.ScriptSettings.Where(item => scriptIds.Contains(item.ScriptDefinitionId)).ExecuteDeleteAsync(ct); await context.ScriptTranslations.Where(item => scriptIds.Contains(item.ScriptDefinitionId)).ExecuteDeleteAsync(ct); await context.ScriptEditHistories.Where(item => scriptIds.Contains(item.ScriptDefinitionId)).ExecuteDeleteAsync(ct);
        foreach (var script in scripts) Bump(script); await context.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(new { success = true, revision = scripts[0].RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpGet("/StoreScript/GetScriptHistoryAjax")]
    public async Task<IActionResult> GetScriptHistoryAjax(int folderId, CancellationToken ct) { var id = await context.StoreScripts.Where(item => item.StoreCodeFolderId == folderId).Select(item => (int?)item.Id).FirstOrDefaultAsync(ct); if (!id.HasValue) return Ok(new { success = true, items = Array.Empty<object>() }); var items = await context.ScriptEditHistories.AsNoTracking().Where(item => item.ScriptDefinitionId == id).OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(400).Select(item => new { id = item.Id, entityType = item.EntityType, entityId = item.EntityId, field = item.Field, oldValue = item.OldValue, newValue = item.NewValue, isRestoreAction = item.IsRestoreAction, createdAt = item.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"), createdByName = item.CreatedByName ?? "" }).ToListAsync(ct); return Ok(new { success = true, items }); }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/RestoreHistoryEntryAjax")]
    public async Task<IActionResult> RestoreHistoryEntryAjax([FromForm] int id, CancellationToken ct)
    {
        var history = await context.ScriptEditHistories.FindAsync([id], ct); if (history is null) return NotFound();
        var script = await context.StoreScripts.FindAsync([history.ScriptDefinitionId], ct); if (script is null) return NotFound();
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        var restored = await ApplyHistoryValue(history, script, ct);
        if (!restored) { await tx.RollbackAsync(ct); return BadRequest(new { message = "لا يمكن استرداد هذا الحقل." }); }
        History(history.ScriptDefinitionId, history.EntityType, history.EntityId, history.Field, history.NewValue, history.OldValue, true);
        Bump(script); await context.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(new { success = true, revision = script.RevisionStamp, restoredValue = history.OldValue });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SetActiveAjax")]
    public async Task<IActionResult> SetActiveAjax([FromForm] int id, [FromForm] bool? isActive, CancellationToken ct)
    {
        var script = await context.StoreScripts.FindAsync([id], ct); if (script is null || script.IsDeleted) return NotFound(); var activate = isActive ?? true; if (script.IsActive == activate) return Ok(new { success = true, script.IsActive, revision = script.RevisionStamp });
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        if (activate) { var siblings = await context.StoreScripts.Where(item => item.Id != id && item.ManufacturingCompanyId == script.ManufacturingCompanyId && item.Platform == script.Platform && item.IsActive && !item.IsDeleted).ToListAsync(ct); foreach (var sibling in siblings) { Track(sibling.Id, "ScriptDefinition", sibling.Id, "IsActive", "True", "False"); sibling.IsActive = false; Bump(sibling); } }
        Track(script.Id, "ScriptDefinition", script.Id, "IsActive", script.IsActive.ToString(), activate.ToString()); script.IsActive = activate; Bump(script); await context.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(new { success = true, script.IsActive, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/SaveSequenceAjax")]
    public async Task<IActionResult> SaveSequenceAjax([FromForm] int subCategoryId, [FromForm] string? nodeType, [FromForm] int? categoryId, [FromForm] string? maleText, [FromForm] string? femaleText, CancellationToken ct)
    {
        int definitionId; string entityType; int entityId; IQueryable<ScriptMessage> messageQuery;
        if (string.Equals(nodeType, "Category", StringComparison.OrdinalIgnoreCase) && categoryId.HasValue) { var category = await context.ScriptCategories.FindAsync([categoryId.Value], ct); if (category is null) return NotFound(); if (await context.ScriptSubCategories.AnyAsync(item => item.ScriptCategoryId == category.Id && item.ParentSubCategoryId == null && !item.IsDeleted, ct)) return BadRequest(new { message = "لا يمكن إضافة رسائل لعنصر يحتوي أزرار فرعية." }); definitionId = category.ScriptDefinitionId; entityType = "ScriptCategory"; entityId = category.Id; messageQuery = context.ScriptMessages.Where(item => item.ScriptCategoryId == category.Id); Touch(category); }
        else { var node = await context.ScriptSubCategories.Include(item => item.ScriptCategory).FirstOrDefaultAsync(item => item.Id == subCategoryId, ct); if (node?.ScriptCategory is null) return NotFound(); if (await context.ScriptSubCategories.AnyAsync(item => item.ParentSubCategoryId == node.Id && !item.IsDeleted, ct)) return BadRequest(new { message = "لا يمكن إضافة رسائل لعنصر يحتوي أزرار فرعية." }); definitionId = node.ScriptCategory.ScriptDefinitionId; entityType = "ScriptSubCategory"; entityId = node.Id; messageQuery = context.ScriptMessages.Where(item => item.ScriptSubCategoryId == node.Id); Touch(node); }
        var existing = await messageQuery.ToListAsync(ct); var previousMale = string.Join('\n', existing.Where(item => item.Gender != "F").OrderBy(item => item.StepOrder).Select(item => item.Text)); var previousFemale = string.Join('\n', existing.Where(item => item.Gender == "F").OrderBy(item => item.StepOrder).Select(item => item.Text)); context.ScriptMessages.RemoveRange(existing); AddSequence(entityType == "ScriptCategory" ? entityId : null, entityType == "ScriptSubCategory" ? entityId : null, maleText, femaleText); var script = await context.StoreScripts.FindAsync([definitionId], ct); Track(script!.Id, entityType, entityId, "MaleSequence", previousMale, maleText); Track(script.Id, entityType, entityId, "FemaleSequence", previousFemale, femaleText); Bump(script); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script.RevisionStamp });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/StoreScript/ImportSeedAjax")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> ImportSeedAjax([FromForm] int folderId, [FromForm] string? seedJson, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(seedJson)) return BadRequest(new { message = "بيانات الاستيراد مطلوبة." });
        ScriptSeedInput? seed; try { seed = JsonSerializer.Deserialize<ScriptSeedInput>(seedJson, JsonOptions); } catch (JsonException exception) { return BadRequest(new { message = "تعذر قراءة ملف الاستيراد: " + exception.Message }); }
        if (seed is null || seed.Buttons.Count == 0) return BadRequest(new { message = "ملف الاستيراد لا يحتوي على أزرار." });
        var platform = Platforms.FirstOrDefault(item => item.Equals(seed.Platform, StringComparison.OrdinalIgnoreCase)); if (platform is null) return BadRequest(new { message = "المنصة يجب أن تكون Meta أو WhatsApp." });
        var script = await ByFolder(folderId, ct); if (script is null) return NotFound(); if (!script.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "منصة الملف لا تطابق منصة السكربت." });
        if (seed.Buttons.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Label))) return BadRequest(new { message = "يوجد زر رئيسي بلا مفتاح أو اسم." });
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        var categoryIds = await context.ScriptCategories.Where(item => item.ScriptDefinitionId == script.Id).Select(item => item.Id).ToListAsync(ct);
        var subIds = await context.ScriptSubCategories.Where(item => categoryIds.Contains(item.ScriptCategoryId)).Select(item => item.Id).ToListAsync(ct);
        await context.ScriptMessages.Where(item => item.ScriptCategoryId != null && categoryIds.Contains(item.ScriptCategoryId.Value) || item.ScriptSubCategoryId != null && subIds.Contains(item.ScriptSubCategoryId.Value)).ExecuteDeleteAsync(ct);
        await context.ScriptSubCategories.Where(item => subIds.Contains(item.Id)).ExecuteDeleteAsync(ct);
        await context.ScriptCategories.Where(item => categoryIds.Contains(item.Id)).ExecuteDeleteAsync(ct);
        await context.ScriptThemeTokens.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct);
        await context.ScriptTargets.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct);
        var theme = new[] { ("BACKGROUND", seed.Settings.BackgroundColor), ("HOVER", seed.Settings.HoverColor), ("PROGRESS", seed.Settings.ProgressColor), ("STOP", seed.Settings.StopColor) };
        context.ScriptThemeTokens.AddRange(theme.Where(item => !string.IsNullOrWhiteSpace(item.Item2)).Select((item, index) => new ScriptThemeToken { ScriptDefinitionId = script.Id, Key = item.Item1, Value = item.Item2!.Trim(), SortOrder = index }));
        var targets = seed.Targets.Where(item => TargetKinds.Contains(item.Kind) && !string.IsNullOrWhiteSpace(item.Value)).GroupBy(item => item.Kind + "\0" + item.Value.Trim(), StringComparer.OrdinalIgnoreCase).Select(item => item.First()).ToList();
        if (targets.Count != seed.Targets.Count) { await tx.RollbackAsync(ct); return BadRequest(new { message = "قائمة المعرفات غير صحيحة أو مكررة." }); }
        var targetValues = targets.Select(item => item.Value.Trim()).ToArray();
        if (await context.ScriptTargets.AnyAsync(item => item.ScriptDefinitionId != script.Id && !item.IsDeleted && targetValues.Contains(item.Value), ct)) { await tx.RollbackAsync(ct); return Conflict(new { message = "أحد المعرفات مستخدم بالفعل من متجر آخر." }); }
        context.ScriptTargets.AddRange(targets.Select(item => new ScriptTarget { ScriptDefinitionId = script.Id, Kind = item.Kind, Value = item.Value.Trim(), SortOrder = item.SortOrder }));
        var categoryCount = 0; var subCount = 0; var lineCount = 0;
        foreach (var (button, index) in seed.Buttons.Select((value, index) => (value, index)))
        {
            var category = new ScriptCategory { ScriptDefinitionId = script.Id, Key = button.Key.Trim(), Label = button.Label.Trim(), Icon = Clean(button.Icon) ?? "•", IconKind = Clean(button.IconKind) ?? "emoji", SortOrder = button.SortOrder != 0 ? button.SortOrder : index, IsEnabled = true, CreatedAt = IstanbulTimeHelper.Now, UpdatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name, UpdatedByUserId = User.GetUserId(), UpdatedByName = User.Identity?.Name };
            context.ScriptCategories.Add(category); await context.SaveChangesAsync(ct); categoryCount++;
            foreach (var (child, childIndex) in button.SubButtons.Select((value, childIndex) => (value, childIndex))) await ImportSubButton(child, childIndex, category.Id, null);
        }
        async Task ImportSubButton(ScriptSeedSubButtonInput input, int index, int categoryId, int? parentId)
        {
            if (string.IsNullOrWhiteSpace(input.Key) || string.IsNullOrWhiteSpace(input.Label)) throw new InvalidOperationException("يوجد زر فرعي بلا مفتاح أو اسم.");
            var node = new ScriptSubCategory { ScriptCategoryId = categoryId, ParentSubCategoryId = parentId, Key = input.Key.Trim(), Label = input.Label.Trim(), Icon = Clean(input.Icon) ?? "•", IconKind = Clean(input.IconKind) ?? "emoji", SortOrder = input.SortOrder != 0 ? input.SortOrder : index, IsEnabled = true, CreatedAt = IstanbulTimeHelper.Now, UpdatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name, UpdatedByUserId = User.GetUserId(), UpdatedByName = User.Identity?.Name };
            context.ScriptSubCategories.Add(node); await context.SaveChangesAsync(ct); subCount++;
            var both = !string.IsNullOrWhiteSpace(input.Male) && !string.IsNullOrWhiteSpace(input.Female);
            lineCount += AddSeedLines(node.Id, both ? "M" : "", input.Male); lineCount += AddSeedLines(node.Id, both ? "F" : "", input.Female);
            foreach (var (child, childIndex) in input.SubButtons.Select((value, childIndex) => (value, childIndex))) await ImportSubButton(child, childIndex, categoryId, node.Id);
        }
        script.IsActive = false; Bump(script); History(script.Id, "ScriptDefinition", script.Id, "Imported", null, $"{seed.SourceScriptId} — {categoryCount}/{subCount}/{lineCount}"); await context.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(new { success = true, scriptId = script.Id, platform = script.Platform, script.IsActive, revision = script.RevisionStamp, primaryCount = categoryCount, secondaryCount = subCount, lineCount });
    }

    private async Task<StoreScript?> LoadTree(int id, bool liveOnly, CancellationToken ct) => await context.StoreScripts.AsSplitQuery().Include(item => item.Targets).Include(item => item.ThemeTokens).Include(item => item.Settings).Include(item => item.Countries).ThenInclude(item => item.Values).Include(item => item.Categories).ThenInclude(item => item.Messages).Include(item => item.Categories).ThenInclude(item => item.SubCategories).ThenInclude(item => item.Messages).Include(item => item.Translations).FirstOrDefaultAsync(item => item.Id == id && (!liveOnly || item.IsActive && !item.IsDeleted), ct);
    private object Tree(StoreScript script, bool liveOnly) => new { script.Id, script.StoreCodeFolderId, script.ManufacturingCompanyId, script.Platform, script.EngineVersion, revision = script.RevisionStamp, script.IsActive, targets = script.Targets.Where(item => !liveOnly || !item.IsDeleted).OrderBy(item => item.SortOrder), theme = script.ThemeTokens.OrderBy(item => item.SortOrder), settings = script.Settings.OrderBy(item => item.SortOrder), countries = script.Countries.Where(item => !liveOnly || item.IsEnabled && !item.IsDeleted).OrderBy(item => item.SortOrder).Select(item => new { item.Id, item.Code, item.Label, item.FlagHex, item.IsEnabled, values = item.Values.ToDictionary(value => value.Key, value => value.Value) }), categories = script.Categories.Where(item => !liveOnly || item.IsEnabled && !item.IsDeleted).OrderBy(item => item.SortOrder).Select(item => new { item.Id, item.Key, item.Label, item.Icon, item.IconKind, item.SortOrder, item.IsEnabled, messages = item.Messages.OrderBy(message => message.Phase).ThenBy(message => message.StepOrder), subCategories = item.SubCategories.Where(node => !liveOnly || node.IsEnabled && !node.IsDeleted).OrderBy(node => node.SortOrder).Select(node => new { node.Id, node.ParentSubCategoryId, node.Key, node.Label, node.Icon, node.IconKind, node.ColorToken, node.IsCountryScoped, node.SortOrder, node.IsEnabled, messages = node.Messages.OrderBy(message => message.Phase).ThenBy(message => message.StepOrder) }) }), translations = script.Translations.Where(item => !liveOnly || !item.IsDeleted) };
    private async Task<StoreScript?> ByFolder(int id, CancellationToken ct) => await context.StoreScripts.FirstOrDefaultAsync(item => item.StoreCodeFolderId == id && !item.IsDeleted, ct);
    private bool CanManage() => User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector");
    private async Task<bool> CanAccess(int companyId, CancellationToken ct) => CanManage() || await context.EmployeeManufacturingCompanies.AnyAsync(item => item.ApplicationUserId == (User.GetUserId() ?? "") && item.ManufacturingCompanyId == companyId && item.CanSeeManufacturingCompany, ct);
    private async Task<IActionResult> ScriptFile(string name, CancellationToken ct) { var path = Path.Combine(environment.ContentRootPath, "ScriptAssets", name); if (!System.IO.File.Exists(path)) return StatusCode(503, "script asset is not deployed yet"); return Content(await System.IO.File.ReadAllTextAsync(path, ct), "application/javascript"); }
    private static string? ValidateNode(string key, string label, string icon, string iconKind, int maxLabel) { if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > 64) return "المفتاح غير صحيح."; if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > maxLabel) return "العنوان غير صحيح."; if (string.IsNullOrWhiteSpace(icon) || icon.Trim().Length > 64) return "الأيقونة غير صحيحة."; return IconKinds.Contains(iconKind.Trim()) ? null : "نوع الأيقونة غير صحيح."; }
    private static List<T>? ParseList<T>(string? json) { try { return JsonSerializer.Deserialize<List<T>>(json ?? "[]", JsonOptions) ?? []; } catch (JsonException) { return null; } }
    private static bool ValidPairs(List<KeyValueOrder> items, int keyMax, int valueMax) => items.All(item => !string.IsNullOrWhiteSpace(item.Key) && item.Key.Trim().Length <= keyMax && !string.IsNullOrEmpty(item.Value) && item.Value.Length <= valueMax) && items.Select(item => item.Key.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == items.Count;
    private async Task<IActionResult> SetDeleted(string type, int id, bool deleted, CancellationToken ct)
    {
        StoreScript? script;
        if (type == "Category") { var item = await context.ScriptCategories.FindAsync([id], ct); if (item is null) return NotFound(); script = await context.StoreScripts.FindAsync([item.ScriptDefinitionId], ct); Track(script!.Id, "ScriptCategory", item.Id, "IsDeleted", item.IsDeleted.ToString(), deleted.ToString()); item.IsDeleted = deleted; item.DeletedAt = deleted ? IstanbulTimeHelper.Now : null; item.DeletedByUserId = deleted ? User.GetUserId() : null; item.DeletedByName = deleted ? User.Identity?.Name : null; Touch(item); }
        else if (type == "SubCategory") { var item = await context.ScriptSubCategories.Include(row => row.ScriptCategory).FirstOrDefaultAsync(row => row.Id == id, ct); if (item?.ScriptCategory is null) return NotFound(); script = await context.StoreScripts.FindAsync([item.ScriptCategory.ScriptDefinitionId], ct); Track(script!.Id, "ScriptSubCategory", item.Id, "IsDeleted", item.IsDeleted.ToString(), deleted.ToString()); item.IsDeleted = deleted; item.DeletedAt = deleted ? IstanbulTimeHelper.Now : null; item.DeletedByUserId = deleted ? User.GetUserId() : null; item.DeletedByName = deleted ? User.Identity?.Name : null; Touch(item); }
        else if (type == "Country") { var item = await context.ScriptCountries.FindAsync([id], ct); if (item is null) return NotFound(); script = await context.StoreScripts.FindAsync([item.ScriptDefinitionId], ct); Track(script!.Id, "ScriptCountry", item.Id, "IsDeleted", item.IsDeleted.ToString(), deleted.ToString()); item.IsDeleted = deleted; }
        else if (type == "Definition") { script = await context.StoreScripts.FindAsync([id], ct); if (script is null) return NotFound(); Track(script.Id, "ScriptDefinition", script.Id, "IsDeleted", script.IsDeleted.ToString(), deleted.ToString()); script.IsDeleted = deleted; script.DeletedAt = deleted ? IstanbulTimeHelper.Now : null; script.DeletedByUserId = deleted ? User.GetUserId() : null; script.DeletedByName = deleted ? User.Identity?.Name : null; if (deleted) script.IsActive = false; }
        else return BadRequest(new { message = "نوع العنصر غير معروف." });
        Bump(script!); await context.SaveChangesAsync(ct); return Ok(new { success = true, revision = script!.RevisionStamp });
    }
    private async Task<bool> ApplyHistoryValue(ScriptEditHistory history, StoreScript script, CancellationToken ct)
    {
        static bool Bool(string? value) => bool.TryParse(value, out var parsed) && parsed;
        static int Int(string? value, int fallback) => int.TryParse(value, out var parsed) ? parsed : fallback;
        if (history.EntityType == "ScriptDefinition")
        {
            if (history.Field == "IsActive") { script.IsActive = Bool(history.OldValue); return true; }
            if (history.Field == "IsDeleted") { script.IsDeleted = Bool(history.OldValue); script.DeletedAt = script.IsDeleted ? IstanbulTimeHelper.Now : null; return true; }
            if (history.Field == "Notes") { script.Notes = history.OldValue; return true; }
            if (history.Field == "Theme") { var values = ParseList<KeyValueOrder>(history.OldValue); if (values is null) return false; await context.ScriptThemeTokens.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); context.ScriptThemeTokens.AddRange(values.Select(item => new ScriptThemeToken { ScriptDefinitionId = script.Id, Key = item.Key, Value = item.Value, SortOrder = item.SortOrder })); return true; }
            if (history.Field == "Targets") { var values = ParseList<TargetInput>(history.OldValue); if (values is null) return false; await context.ScriptTargets.Where(item => item.ScriptDefinitionId == script.Id).ExecuteDeleteAsync(ct); context.ScriptTargets.AddRange(values.Select(item => new ScriptTarget { ScriptDefinitionId = script.Id, Kind = item.Kind, Value = item.Value, SortOrder = item.SortOrder })); return true; }
            return false;
        }
        if (history.EntityType == "ScriptCategory")
        {
            var item = await context.ScriptCategories.FirstOrDefaultAsync(row => row.Id == history.EntityId && row.ScriptDefinitionId == script.Id, ct); if (item is null) return false;
            switch (history.Field) { case "Key": item.Key = history.OldValue ?? item.Key; break; case "Label": item.Label = history.OldValue ?? item.Label; break; case "Icon": item.Icon = history.OldValue ?? item.Icon; break; case "IconKind": item.IconKind = history.OldValue ?? item.IconKind; break; case "SortOrder": item.SortOrder = Int(history.OldValue, item.SortOrder); break; case "IsEnabled": item.IsEnabled = Bool(history.OldValue); break; case "IsDeleted": item.IsDeleted = Bool(history.OldValue); item.DeletedAt = item.IsDeleted ? IstanbulTimeHelper.Now : null; break; case "MaleSequence": await RestoreSequence(item.Id, null, history.OldValue, "M", ct); break; case "FemaleSequence": await RestoreSequence(item.Id, null, history.OldValue, "F", ct); break; default: return false; } Touch(item); return true;
        }
        if (history.EntityType == "ScriptSubCategory")
        {
            var item = await context.ScriptSubCategories.Include(row => row.ScriptCategory).FirstOrDefaultAsync(row => row.Id == history.EntityId && row.ScriptCategory!.ScriptDefinitionId == script.Id, ct); if (item is null) return false;
            switch (history.Field) { case "Key": item.Key = history.OldValue ?? item.Key; break; case "Label": item.Label = history.OldValue ?? item.Label; break; case "Icon": item.Icon = history.OldValue ?? item.Icon; break; case "IconKind": item.IconKind = history.OldValue ?? item.IconKind; break; case "ColorToken": item.ColorToken = history.OldValue; break; case "SortOrder": item.SortOrder = Int(history.OldValue, item.SortOrder); break; case "IsCountryScoped": item.IsCountryScoped = Bool(history.OldValue); break; case "IsEnabled": item.IsEnabled = Bool(history.OldValue); break; case "IsDeleted": item.IsDeleted = Bool(history.OldValue); item.DeletedAt = item.IsDeleted ? IstanbulTimeHelper.Now : null; break; case "ParentSubCategoryId": item.ParentSubCategoryId = string.IsNullOrWhiteSpace(history.OldValue) ? null : Int(history.OldValue, 0); break; case "MaleSequence": await RestoreSequence(null, item.Id, history.OldValue, "M", ct); break; case "FemaleSequence": await RestoreSequence(null, item.Id, history.OldValue, "F", ct); break; case "Messages": var messages = ParseList<MessageInput>(history.OldValue); if (messages is null) return false; var countries = await context.ScriptCountries.Where(row => row.ScriptDefinitionId == script.Id && !row.IsDeleted).ToDictionaryAsync(row => row.Code, row => row.Id, StringComparer.OrdinalIgnoreCase, ct); await context.ScriptMessages.Where(row => row.ScriptSubCategoryId == item.Id).ExecuteDeleteAsync(ct); context.ScriptMessages.AddRange(messages.Select(message => new ScriptMessage { ScriptSubCategoryId = item.Id, ScriptCountryId = message.CountryCode is null ? null : countries.GetValueOrDefault(message.CountryCode), Phase = message.Phase, StepOrder = message.StepOrder, Gender = message.Gender ?? string.Empty, Text = message.Text })); break; default: return false; } Touch(item); return true;
        }
        if (history.EntityType == "ScriptCountry")
        {
            var item = await context.ScriptCountries.Include(row => row.Values).FirstOrDefaultAsync(row => row.Id == history.EntityId && row.ScriptDefinitionId == script.Id, ct); if (item is null) return false;
            switch (history.Field) { case "Code": item.Code = history.OldValue ?? item.Code; break; case "Label": item.Label = history.OldValue ?? item.Label; break; case "FlagHex": item.FlagHex = history.OldValue ?? item.FlagHex; break; case "SortOrder": item.SortOrder = Int(history.OldValue, item.SortOrder); break; case "IsEnabled": item.IsEnabled = Bool(history.OldValue); break; case "IsDeleted": item.IsDeleted = Bool(history.OldValue); break; case "Values": var values = ParseList<KeyValueOrder>(history.OldValue); if (values is null) return false; context.ScriptCountryValues.RemoveRange(item.Values); context.ScriptCountryValues.AddRange(values.Select(value => new ScriptCountryValue { ScriptCountryId = item.Id, Key = value.Key, Value = value.Value })); break; default: return false; } return true;
        }
        return false;
    }
    private int AddSeedLines(int subCategoryId, string gender, string? text) { var lines = (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); for (var index = 0; index < lines.Length; index++) context.ScriptMessages.Add(new ScriptMessage { ScriptSubCategoryId = subCategoryId, Phase = 0, StepOrder = index, Gender = gender, Text = lines[index] }); return lines.Length; }
    private async Task RestoreSequence(int? categoryId, int? subCategoryId, string? text, string gender, CancellationToken ct) { var query = context.ScriptMessages.Where(item => categoryId.HasValue ? item.ScriptCategoryId == categoryId : item.ScriptSubCategoryId == subCategoryId); await query.Where(item => item.Gender == gender || gender == "M" && item.Gender == "").ExecuteDeleteAsync(ct); var lines = (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); context.ScriptMessages.AddRange(lines.Select((line, index) => new ScriptMessage { ScriptCategoryId = categoryId, ScriptSubCategoryId = subCategoryId, Phase = 0, StepOrder = index, Gender = gender, Text = line })); }
    private void History(int scriptId, string entity, int entityId, string field, string? oldValue, string? newValue, bool restore = false) => context.ScriptEditHistories.Add(new ScriptEditHistory { ScriptDefinitionId = scriptId, EntityType = entity, EntityId = entityId, Field = field, OldValue = oldValue, NewValue = newValue, IsRestoreAction = restore, CreatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name });
    private void Track(int scriptId, string entity, int entityId, string field, string? oldValue, string? newValue) { if (!string.Equals(oldValue, newValue, StringComparison.Ordinal)) History(scriptId, entity, entityId, field, oldValue, newValue); }
    private void Bump(StoreScript script) { script.RevisionStamp = Math.Max(script.RevisionStamp + 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); script.UpdatedAt = IstanbulTimeHelper.Now; script.UpdatedByUserId = User.GetUserId(); script.UpdatedByName = User.Identity?.Name; }
    private void Touch(ScriptCategory item) { item.UpdatedAt = IstanbulTimeHelper.Now; item.UpdatedByUserId = User.GetUserId(); item.UpdatedByName = User.Identity?.Name; }
    private void Touch(ScriptSubCategory item) { item.UpdatedAt = IstanbulTimeHelper.Now; item.UpdatedByUserId = User.GetUserId(); item.UpdatedByName = User.Identity?.Name; }
    private void AddSequence(int? categoryId, int? subCategoryId, string? male, string? female) { var step = 0; var both = !string.IsNullOrWhiteSpace(male) && !string.IsNullOrWhiteSpace(female); foreach (var (gender, text) in new[] { (both ? "M" : "", male), (both ? "F" : "", female) }) foreach (var line in (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) context.ScriptMessages.Add(new ScriptMessage { ScriptCategoryId = categoryId, ScriptSubCategoryId = subCategoryId, Phase = 0, StepOrder = step++, Gender = gender, Text = line }); }
    private static string NormalizeGender(string? value) => value?.Trim().ToUpperInvariant() is "F" ? "F" : "M";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record MessageInput(string? CountryCode, int Phase, int StepOrder, string? Gender, string Text);
public sealed record KeyValueOrder(string Key, string Value, int SortOrder);
public sealed record TargetInput(string Kind, string Value, int SortOrder);
public sealed class ScriptSeedInput { public int SourceScriptId { get; set; } public string Platform { get; set; } = string.Empty; public ScriptSeedSettingsInput Settings { get; set; } = new(); public List<TargetInput> Targets { get; set; } = []; public List<ScriptSeedButtonInput> Buttons { get; set; } = []; }
public sealed class ScriptSeedSettingsInput { public string? BackgroundColor { get; set; } public string? HoverColor { get; set; } public string? ProgressColor { get; set; } public string? StopColor { get; set; } }
public sealed class ScriptSeedButtonInput { public string Key { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; public string? Icon { get; set; } public string? IconKind { get; set; } public int SortOrder { get; set; } public List<ScriptSeedSubButtonInput> SubButtons { get; set; } = []; }
public sealed class ScriptSeedSubButtonInput { public string Key { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; public string? Icon { get; set; } public string? IconKind { get; set; } public int SortOrder { get; set; } public string? Male { get; set; } public string? Female { get; set; } public List<ScriptSeedSubButtonInput> SubButtons { get; set; } = []; }
