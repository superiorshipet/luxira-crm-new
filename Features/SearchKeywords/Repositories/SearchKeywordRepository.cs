using Microsoft.EntityFrameworkCore;
using Luxira.Api.Data;
using Luxira.Api.Features.SearchKeywords.Models;

namespace Luxira.Api.Features.SearchKeywords.Repositories;

public class SearchKeywordRepository
{
    private readonly ApplicationDbContext _context;

    public SearchKeywordRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SearchKeywordOption>> SearchAsync(
        string? search = null,
        string? targetType = null,
        string? category = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = _context.SearchKeywordOptions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(k => k.Keyword.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(targetType))
        {
            query = query.Where(k => k.TargetType == targetType);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(k => k.Category == category);
        }

        if (isActive.HasValue)
        {
            query = query.Where(k => k.IsActive == isActive.Value);
        }

        return await query.OrderBy(k => k.SortOrder).ThenBy(k => k.Keyword).ToListAsync(ct);
    }

    public async Task<SearchKeywordOption> AddAsync(SearchKeywordOption option, CancellationToken ct = default)
    {
        var result = await _context.SearchKeywordOptions.AddAsync(option, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }
}
