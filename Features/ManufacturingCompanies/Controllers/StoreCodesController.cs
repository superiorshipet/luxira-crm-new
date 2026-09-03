using System.Globalization;
using System.Security.Claims;
using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/manufacturing-companies/store-codes")]
[Route("StoreCodes")]
public class StoreCodesController(ApplicationDbContext context) : ControllerBase
{
    private const string Managers = "Admin,ExecutiveDirector";

    [HttpGet]
    [HttpGet("/StoreCodes/GetStoreCodes")]
    public async Task<IActionResult> GetStoreCodes([FromQuery] int? manufacturingCompanyId, CancellationToken ct)
    {
        var query = context.StoreCodeFolders.AsNoTracking().AsQueryable();
        if (manufacturingCompanyId is > 0)
            query = query.Where(x => x.ManufacturingCompanyId == manufacturingCompanyId);
        return Ok(await query.OrderByDescending(x => x.UpdatedAt).ToListAsync(ct));
    }

    [HttpGet("/StoreCodes/Index")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> Index(int? manufacturingCompanyId, string? fromDate, string? toDate, CancellationToken ct)
    {
        var from = ParseDate(fromDate);
        var to = ParseDate(toDate);
        var folders = await GetFolderRows(false, manufacturingCompanyId, from, to, ct);
        return Ok(new
        {
            manufacturingCompanyId,
            fromDate,
            toDate,
            manufacturingCompanies = await GetCompanyOptions(ct),
            storeGroups = await GetStoreGroups(manufacturingCompanyId, from, to, folders, ct),
            folders,
            trashFolders = await GetFolderRows(true, manufacturingCompanyId, from, to, ct)
        });
    }

    [HttpGet("/StoreCodes/GetFoldersAjax")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> GetFoldersAjax(int? manufacturingCompanyId, string? fromDate, string? toDate, CancellationToken ct)
    {
        var from = ParseDate(fromDate);
        var to = ParseDate(toDate);
        var folders = await GetFolderRows(false, manufacturingCompanyId, from, to, ct);
        return Ok(new
        {
            success = true,
            storeGroups = await GetStoreGroups(manufacturingCompanyId, from, to, folders, ct),
            folders,
            trashFolders = await GetFolderRows(true, manufacturingCompanyId, from, to, ct)
        });
    }

    [HttpGet("/StoreCodes/GetTrashAjax")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> GetTrashAjax(int? manufacturingCompanyId, string? fromDate, string? toDate, CancellationToken ct) =>
        Ok(new
        {
            success = true,
            items = await GetFolderRows(true, manufacturingCompanyId, ParseDate(fromDate), ParseDate(toDate), ct)
        });

    [HttpGet("/StoreCodes/GetFolderContentAjax")]
    public async Task<IActionResult> GetFolderContentAjax(int id, CancellationToken ct)
    {
        var folder = await context.StoreCodeFolders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (folder is null) return Fail("المجلد غير موجود.");
        if (!await CanAccessCompany(folder.ManufacturingCompanyId, ct))
            return Fail("ليس لديك صلاحية لنسخ أكواد هذا المتجر.");
        return Ok(new { success = true, content = folder.Content ?? string.Empty });
    }

    [HttpGet("/StoreCodes/GetMyStoreCodesAjax")]
    public async Task<IActionResult> GetMyStoreCodesAjax(CancellationToken ct)
    {
        var query = context.StoreCodeFolders.AsNoTracking().Where(x => !x.IsDeleted);
        if (!CanManage())
        {
            var userId = CurrentUserId();
            var allowed = context.EmployeeManufacturingCompanies.AsNoTracking()
                .Where(x => x.ApplicationUserId == userId && x.CanSeeManufacturingCompany)
                .Select(x => x.ManufacturingCompanyId);
            query = query.Where(x => allowed.Contains(x.ManufacturingCompanyId));
        }

        var raw = await query.OrderBy(x => x.ManufacturingCompany!.Name).ThenBy(x => x.FolderName).ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.FolderName,
                x.PageType,
                x.ManufacturingCompanyId,
                CompanyName = x.ManufacturingCompany != null ? x.ManufacturingCompany.Name : string.Empty,
                CompanyImage = x.ManufacturingCompany != null ? x.ManufacturingCompany.ImageUrl ?? string.Empty : string.Empty,
                x.UpdatedAt
            }).ToListAsync(ct);
        var items = raw.Select(x => new
        {
            id = x.Id,
            folderName = string.IsNullOrWhiteSpace(x.FolderName) ? "بدون اسم" : x.FolderName,
            pageType = x.PageType ?? string.Empty,
            manufacturingCompanyId = x.ManufacturingCompanyId,
            manufacturingCompanyName = x.CompanyName,
            manufacturingCompanyImageUrl = x.CompanyImage,
            updatedAt = x.UpdatedAt.ToString("yyyy/MM/dd hh:mm tt")
        }).ToList();
        var storeGroups = items.GroupBy(x => new
            {
                x.manufacturingCompanyId,
                x.manufacturingCompanyName,
                x.manufacturingCompanyImageUrl
            })
            .Select(g => new
            {
                g.Key.manufacturingCompanyId,
                g.Key.manufacturingCompanyName,
                g.Key.manufacturingCompanyImageUrl,
                folderCount = g.Count(),
                folders = g.Select(x => new { x.id, x.folderName, x.pageType, x.updatedAt }).ToList()
            }).OrderBy(x => x.manufacturingCompanyName).ThenBy(x => x.manufacturingCompanyId).ToList();
        return Ok(new { success = true, storeGroups, items });
    }

    [HttpGet("/StoreCodes/GetHistoryAjax")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> GetHistoryAjax(int? manufacturingCompanyId, string? fromDate, string? toDate, CancellationToken ct)
    {
        var from = ParseDate(fromDate);
        var to = ParseDate(toDate);
        var query = context.StoreCodeEditHistories.AsNoTracking().AsQueryable();
        if (manufacturingCompanyId is > 0) query = query.Where(x => x.ManufacturingCompanyId == manufacturingCompanyId);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value.Date);
        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < end);
        }
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(400)
            .Select(x => new
            {
                x.Id,
                x.StoreCodeFolderId,
                FileName = x.FileName ?? string.Empty,
                x.LineNumber,
                OldValue = x.OldValue ?? string.Empty,
                NewValue = x.NewValue ?? string.Empty,
                x.IsRestoreAction,
                x.CreatedAt,
                CreatedByName = x.CreatedByName ?? string.Empty
            }).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpPost("/StoreCodes/CreateStoreGroupAjax")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> CreateStoreGroupAjax([FromForm] int manufacturingCompanyId, CancellationToken ct)
    {
        if (manufacturingCompanyId <= 0) return Fail("اختر المتجر.");
        var company = await context.ManufacturingCompanies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == manufacturingCompanyId, ct);
        if (company is null) return Fail("اختر متجر صحيح.");
        if (await context.StoreCodeStoreGroups.AnyAsync(x => x.ManufacturingCompanyId == manufacturingCompanyId, ct))
            return Fail("المتجر موجود بالفعل داخل مجلدات الأكواد.");
        context.StoreCodeStoreGroups.Add(NewStoreGroup(manufacturingCompanyId));
        await context.SaveChangesAsync(ct);
        return Ok(new
        {
            success = true,
            message = $"تم إنشاء مجلد المتجر {company.Name}.",
            storeGroups = await GetStoreGroups(ct: ct)
        });
    }

    [HttpPost("/StoreCodes/CreateFolderAjax")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> CreateFolderAjax([FromForm] int manufacturingCompanyId, [FromForm] string? folderName, [FromForm] string? pageType, CancellationToken ct)
    {
        folderName = Normalize(folderName);
        pageType = Normalize(pageType);
        var error = await ValidateFolder(manufacturingCompanyId, folderName, pageType, null, ct);
        if (error.Length > 0) return Fail(error);
        await EnsureStoreGroup(manufacturingCompanyId, ct);
        var now = DateTime.Now;
        var userId = CurrentUserId();
        var userName = User.Identity?.Name;
        context.StoreCodeFolders.Add(new StoreCodeFolder
        {
            ManufacturingCompanyId = manufacturingCompanyId,
            FolderName = folderName,
            PageType = pageType,
            Content = string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = userId,
            CreatedByName = userName,
            UpdatedByUserId = userId,
            UpdatedByName = userName
        });
        await context.SaveChangesAsync(ct);
        return Ok(new
        {
            success = true,
            message = $"تم إنشاء ملف {folderName}.",
            storeGroups = await GetStoreGroups(ct: ct),
            folders = await GetFolderRows(ct: ct)
        });
    }

    [HttpPost("/StoreCodes/UpdateFolderAjax")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> UpdateFolderAjax([FromForm] int id, [FromForm] int manufacturingCompanyId, [FromForm] string? folderName, [FromForm] string? pageType, CancellationToken ct)
    {
        var folder = await context.StoreCodeFolders.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (folder is null) return Fail("المجلد غير موجود.");
        var name = Normalize(folderName);
        var type = Normalize(pageType);
        name = name.Length == 0 ? Normalize(folder.FolderName) : name;
        type = type.Length == 0 ? Normalize(folder.PageType) : type;
        var error = await ValidateFolder(manufacturingCompanyId, name, type, id, ct);
        if (error.Length > 0) return Fail(error);
        await EnsureStoreGroup(manufacturingCompanyId, ct);
        folder.ManufacturingCompanyId = manufacturingCompanyId;
        folder.FolderName = name;
        folder.PageType = type;
        Stamp(folder);
        await context.SaveChangesAsync(ct);
        return Ok(new
        {
            success = true,
            message = "تم تعديل المجلد.",
            storeGroups = await GetStoreGroups(ct: ct),
            folders = await GetFolderRows(ct: ct),
            trashFolders = await GetFolderRows(true, ct: ct)
        });
    }

    [HttpPost("/StoreCodes/DeleteFolderAjax")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> DeleteFolderAjax([FromForm] int id, CancellationToken ct)
    {
        var folder = await context.StoreCodeFolders.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (folder is null) return Fail("المجلد غير موجود.");
        folder.IsDeleted = true;
        folder.DeletedAt = DateTime.Now;
        folder.DeletedByUserId = CurrentUserId();
        folder.DeletedByName = User.Identity?.Name;
        Stamp(folder);
        await context.SaveChangesAsync(ct);
        return Ok(new
        {
            success = true,
            message = "تم نقل المجلد إلى سلة المهملات.",
            storeGroups = await GetStoreGroups(ct: ct),
            folders = await GetFolderRows(ct: ct),
            trashFolders = await GetFolderRows(true, ct: ct)
        });
    }

    [HttpPost("/StoreCodes/RestoreFolderAjax")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> RestoreFolderAjax([FromForm] int id, CancellationToken ct)
    {
        var folder = await context.StoreCodeFolders.FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted, ct);
        if (folder is null) return Fail("العنصر غير موجود في سلة المهملات.");
        var name = Normalize(folder.FolderName);
        var duplicate = await context.StoreCodeFolders.AnyAsync(x => x.ManufacturingCompanyId == folder.ManufacturingCompanyId
            && x.FolderName == name && !x.IsDeleted && x.Id != folder.Id, ct);
        if (duplicate) return Fail("لا يمكن الاسترداد لوجود مجلد حالي بنفس الاسم لنفس المتجر.");
        folder.IsDeleted = false;
        folder.DeletedAt = null;
        folder.DeletedByUserId = null;
        folder.DeletedByName = null;
        Stamp(folder);
        await context.SaveChangesAsync(ct);
        return Ok(new
        {
            success = true,
            message = "تم استرداد المجلد.",
            storeGroups = await GetStoreGroups(ct: ct),
            folders = await GetFolderRows(ct: ct),
            trashFolders = await GetFolderRows(true, ct: ct)
        });
    }

    [HttpPost("/StoreCodes/PermanentDeleteFolderAjax")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> PermanentDeleteFolderAjax([FromForm] int id, CancellationToken ct)
    {
        var folder = await context.StoreCodeFolders.FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted, ct);
        if (folder is null) return Fail("العنصر غير موجود في سلة المهملات.");
        context.StoreCodeFolders.Remove(folder);
        await context.SaveChangesAsync(ct);
        return Ok(new
        {
            success = true,
            message = "تم الحذف النهائي.",
            storeGroups = await GetStoreGroups(ct: ct),
            folders = await GetFolderRows(ct: ct),
            trashFolders = await GetFolderRows(true, ct: ct)
        });
    }

    [HttpGet("/StoreCodes/File")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> File(int id, CancellationToken ct)
    {
        var folder = await context.StoreCodeFolders.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.FolderName,
                x.ManufacturingCompanyId,
                ManufacturingCompanyName = x.ManufacturingCompany != null ? x.ManufacturingCompany.Name : string.Empty,
                ManufacturingCompanyImageUrl = x.ManufacturingCompany != null ? x.ManufacturingCompany.ImageUrl ?? string.Empty : string.Empty,
                Content = x.Content ?? string.Empty,
                x.CreatedAt,
                x.UpdatedAt
            }).FirstOrDefaultAsync(ct);
        return folder is null ? NotFound() : Ok(folder);
    }

    [HttpPost("/StoreCodes/SaveCodeAjax")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> SaveCodeAjax([FromForm] int id, [FromForm] string? content, CancellationToken ct)
    {
        var folder = await context.StoreCodeFolders.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (folder is null) return Fail("المجلد غير موجود.");
        var oldContent = folder.Content ?? string.Empty;
        var newContent = content ?? string.Empty;
        if (!string.Equals(oldContent, newContent, StringComparison.Ordinal))
        {
            AddHistory(folder, oldContent, newContent);
            folder.Content = newContent;
            Stamp(folder);
            await context.SaveChangesAsync(ct);
        }
        return Ok(new
        {
            success = true,
            message = "تم الحفظ تلقائيًا",
            folderId = folder.Id,
            updatedAt = folder.UpdatedAt.ToString("yyyy/MM/dd hh:mm tt"),
            updatedAtIso = folder.UpdatedAt.ToString("o")
        });
    }

    [HttpPost("/StoreCodes/RestoreHistoryLineAjax")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> RestoreHistoryLineAjax([FromForm] int id, CancellationToken ct)
    {
        var history = await context.StoreCodeEditHistories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (history is null) return Fail("السجل غير موجود أو الملف محذوف.");
        var folder = await context.StoreCodeFolders.FirstOrDefaultAsync(x => x.Id == history.StoreCodeFolderId && !x.IsDeleted, ct);
        if (folder is null) return Fail("السجل غير موجود أو الملف محذوف.");
        var lines = SplitLines(folder.Content ?? string.Empty).ToList();
        var lineIndex = Math.Max(history.LineNumber - 1, 0);
        while (lines.Count <= lineIndex) lines.Add(string.Empty);
        var current = lines[lineIndex];
        var updated = RestoreChangedText(current, history.OldValue ?? string.Empty, history.NewValue ?? string.Empty);
        if (current == updated) return Ok(new { success = true, message = "القيمة القديمة موجودة بالفعل." });
        lines[lineIndex] = updated;
        context.StoreCodeEditHistories.Add(NewHistory(folder, history.LineNumber, history.NewValue, history.OldValue, true));
        folder.Content = string.Join("\n", lines);
        Stamp(folder);
        await context.SaveChangesAsync(ct);
        return Ok(new
        {
            success = true,
            message = "تم استرداد السطر القديم.",
            folderId = folder.Id,
            updatedAt = folder.UpdatedAt.ToString("yyyy/MM/dd hh:mm tt"),
            updatedAtIso = folder.UpdatedAt.ToString("o")
        });
    }

    private async Task<List<CompanyOption>> GetCompanyOptions(CancellationToken ct) => await context.ManufacturingCompanies.AsNoTracking()
        .OrderBy(x => x.Name).Select(x => new CompanyOption(x.Id, x.Name, x.ImageUrl ?? string.Empty)).ToListAsync(ct);

    private async Task<List<FolderRow>> GetFolderRows(bool isDeleted = false, int? companyId = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var query = context.StoreCodeFolders.AsNoTracking().Where(x => x.IsDeleted == isDeleted);
        if (companyId is > 0) query = query.Where(x => x.ManufacturingCompanyId == companyId);
        if (from.HasValue) query = query.Where(x => x.UpdatedAt >= from.Value.Date);
        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            query = query.Where(x => x.UpdatedAt < end);
        }
        return await query.OrderByDescending(x => isDeleted ? x.DeletedAt ?? x.UpdatedAt : x.UpdatedAt).ThenByDescending(x => x.Id)
            .Select(x => new FolderRow(x.Id,
                string.IsNullOrWhiteSpace(x.FolderName) ? x.ManufacturingCompany != null ? x.ManufacturingCompany.Name : string.Empty : x.FolderName,
                x.PageType ?? string.Empty, x.ManufacturingCompanyId,
                x.ManufacturingCompany != null ? x.ManufacturingCompany.Name : string.Empty,
                x.ManufacturingCompany != null ? x.ManufacturingCompany.ImageUrl ?? string.Empty : string.Empty,
                x.CreatedAt, x.UpdatedAt, x.IsDeleted, x.DeletedAt)).ToListAsync(ct);
    }

    private async Task<List<StoreGroupRow>> GetStoreGroups(int? companyId = null, DateTime? from = null, DateTime? to = null, List<FolderRow>? prepared = null, CancellationToken ct = default)
    {
        var folders = prepared ?? await GetFolderRows(false, companyId, from, to, ct);
        var query = context.StoreCodeStoreGroups.AsNoTracking().AsQueryable();
        if (companyId is > 0) query = query.Where(x => x.ManufacturingCompanyId == companyId);
        var groups = await query.Select(x => new StoreGroupRow
        {
            Id = x.Id,
            ManufacturingCompanyId = x.ManufacturingCompanyId,
            ManufacturingCompanyName = x.ManufacturingCompany != null ? x.ManufacturingCompany.Name : string.Empty,
            ManufacturingCompanyImageUrl = x.ManufacturingCompany != null ? x.ManufacturingCompany.ImageUrl ?? string.Empty : string.Empty,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.CreatedAt
        }).ToListAsync(ct);
        var existing = groups.Select(x => x.ManufacturingCompanyId).ToHashSet();
        var missing = folders.Select(x => x.ManufacturingCompanyId).Distinct().Where(x => !existing.Contains(x)).ToList();
        var companies = await context.ManufacturingCompanies.AsNoTracking().Where(x => missing.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.ImageUrl }).ToListAsync(ct);
        foreach (var company in companies)
        {
            var companyFolders = folders.Where(x => x.ManufacturingCompanyId == company.Id).ToList();
            groups.Add(new StoreGroupRow
            {
                ManufacturingCompanyId = company.Id,
                ManufacturingCompanyName = company.Name,
                ManufacturingCompanyImageUrl = company.ImageUrl ?? string.Empty,
                CreatedAt = companyFolders.Min(x => x.CreatedAt),
                UpdatedAt = companyFolders.Max(x => x.UpdatedAt)
            });
        }
        foreach (var group in groups)
        {
            group.Folders = folders.Where(x => x.ManufacturingCompanyId == group.ManufacturingCompanyId)
                .OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id).ToList();
            if (group.Folders.Count > 0) group.UpdatedAt = group.Folders.Max(x => x.UpdatedAt);
        }
        if (from.HasValue || to.HasValue)
            groups = groups.Where(x => x.Folders.Count > 0 || (!from.HasValue || x.CreatedAt >= from.Value.Date)
                && (!to.HasValue || x.CreatedAt < to.Value.Date.AddDays(1))).ToList();
        return groups.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.ManufacturingCompanyName).ToList();
    }

    private async Task<string> ValidateFolder(int companyId, string name, string pageType, int? currentId, CancellationToken ct)
    {
        if (name.Length == 0) return "اكتب اسم الملف.";
        if (name.Length > 150) return "اسم الملف لا يزيد عن 150 حرف.";
        if (pageType.Length == 0) return "اكتب نوع الصفحة.";
        if (pageType.Length > 100) return "نوع الصفحة لا يزيد عن 100 حرف.";
        if (companyId <= 0) return "اختر المتجر.";
        if (!await context.ManufacturingCompanies.AnyAsync(x => x.Id == companyId, ct)) return "اختر متجر صحيح.";
        var duplicate = await context.StoreCodeFolders.AnyAsync(x => x.ManufacturingCompanyId == companyId && x.FolderName == name
            && !x.IsDeleted && (!currentId.HasValue || x.Id != currentId.Value), ct);
        return duplicate ? "يوجد ملف بنفس الاسم لهذا المتجر بالفعل." : string.Empty;
    }

    private async Task EnsureStoreGroup(int companyId, CancellationToken ct)
    {
        if (!await context.StoreCodeStoreGroups.AnyAsync(x => x.ManufacturingCompanyId == companyId, ct))
            context.StoreCodeStoreGroups.Add(NewStoreGroup(companyId));
    }

    private StoreCodeStoreGroup NewStoreGroup(int companyId) => new()
    {
        ManufacturingCompanyId = companyId,
        CreatedAt = DateTime.Now,
        CreatedByUserId = CurrentUserId(),
        CreatedByName = User.Identity?.Name
    };

    private void AddHistory(StoreCodeFolder folder, string oldContent, string newContent)
    {
        if (string.IsNullOrWhiteSpace(oldContent) && !string.IsNullOrWhiteSpace(newContent)) return;
        var oldLines = SplitLines(oldContent).ToList();
        var newLines = SplitLines(newContent).ToList();
        for (var i = 0; i < Math.Max(oldLines.Count, newLines.Count); i++)
        {
            var oldLine = i < oldLines.Count ? oldLines[i] : string.Empty;
            var newLine = i < newLines.Count ? newLines[i] : string.Empty;
            if (oldLine == newLine || string.IsNullOrWhiteSpace(oldLine) && !string.IsNullOrWhiteSpace(newLine)) continue;
            var changed = ChangedText(oldLine, newLine);
            if (changed.HasValue) context.StoreCodeEditHistories.Add(NewHistory(folder, i + 1, changed.Old, changed.New, false));
        }
    }

    private StoreCodeEditHistory NewHistory(StoreCodeFolder folder, int line, string? oldValue, string? newValue, bool restore) => new()
    {
        StoreCodeFolderId = folder.Id,
        ManufacturingCompanyId = folder.ManufacturingCompanyId,
        FileName = BuildHistoryFileName(folder),
        LineNumber = line,
        OldValue = oldValue ?? string.Empty,
        NewValue = newValue ?? string.Empty,
        IsRestoreAction = restore,
        CreatedAt = DateTime.Now,
        CreatedByUserId = CurrentUserId(),
        CreatedByName = User.Identity?.Name
    };

    private static (string Old, string New, bool HasValue) ChangedText(string oldLine, string newLine)
    {
        if (oldLine == newLine) return (string.Empty, string.Empty, false);
        var prefix = 0;
        while (prefix < Math.Min(oldLine.Length, newLine.Length) && oldLine[prefix] == newLine[prefix]) prefix++;
        var oldEnd = oldLine.Length - 1;
        var newEnd = newLine.Length - 1;
        while (oldEnd >= prefix && newEnd >= prefix && oldLine[oldEnd] == newLine[newEnd]) { oldEnd--; newEnd--; }
        var oldChanged = oldEnd >= prefix ? oldLine.Substring(prefix, oldEnd - prefix + 1).Trim() : string.Empty;
        var newChanged = newEnd >= prefix ? newLine.Substring(prefix, newEnd - prefix + 1).Trim() : string.Empty;
        return oldChanged == newChanged ? (string.Empty, string.Empty, false) : (oldChanged, newChanged, true);
    }

    private static string RestoreChangedText(string current, string oldText, string newText)
    {
        if (newText.Length > 0 && current.Contains(newText, StringComparison.Ordinal))
            return current.Replace(newText, oldText, StringComparison.Ordinal);
        if (newText.Length == 0 && oldText.Length > 0) return string.IsNullOrWhiteSpace(current) ? oldText : $"{current} {oldText}";
        return oldText;
    }

    private static string[] SplitLines(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
    private static string BuildHistoryFileName(StoreCodeFolder folder)
    {
        var name = Normalize(folder.FolderName);
        var pageType = Normalize(folder.PageType);
        name = name.Length == 0 ? "ملف" : name;
        return pageType.Length == 0 ? name : $"{name} - {pageType}";
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var formats = new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "MM/dd/yyyy" };
        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)) return exact;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return parsed;
        return DateTime.TryParse(value, new CultureInfo("ar-EG"), DateTimeStyles.None, out var arabic) ? arabic : null;
    }

    private OkObjectResult Fail(string message) => Ok(new { success = false, message });
    private bool CanManage() => User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector");
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private void Stamp(StoreCodeFolder folder)
    {
        folder.UpdatedAt = DateTime.Now;
        folder.UpdatedByUserId = CurrentUserId();
        folder.UpdatedByName = User.Identity?.Name;
    }

    private async Task<bool> CanAccessCompany(int companyId, CancellationToken ct)
    {
        if (CanManage()) return true;
        var userId = CurrentUserId();
        return userId.Length > 0 && await context.EmployeeManufacturingCompanies.AsNoTracking()
            .AnyAsync(x => x.ApplicationUserId == userId && x.ManufacturingCompanyId == companyId && x.CanSeeManufacturingCompany, ct);
    }

    private sealed record CompanyOption(int Id, string Name, string ImageUrl);
    private sealed record FolderRow(int Id, string FolderName, string PageType, int ManufacturingCompanyId,
        string ManufacturingCompanyName, string ManufacturingCompanyImageUrl, DateTime CreatedAt, DateTime UpdatedAt,
        bool IsDeleted, DateTime? DeletedAt);
    private sealed class StoreGroupRow
    {
        public int Id { get; init; }
        public int ManufacturingCompanyId { get; init; }
        public string ManufacturingCompanyName { get; init; } = string.Empty;
        public string ManufacturingCompanyImageUrl { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; set; }
        public List<FolderRow> Folders { get; set; } = [];
    }
}
