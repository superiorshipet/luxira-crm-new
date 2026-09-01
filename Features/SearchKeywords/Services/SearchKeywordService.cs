using Luxira.Api.Features.SearchKeywords.DTOs;
using Luxira.Api.Features.SearchKeywords.Models;
using Luxira.Api.Features.SearchKeywords.Repositories;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Features.SearchKeywords.Services;

public class SearchKeywordService
{
    private readonly SearchKeywordRepository _repository;

    public SearchKeywordService(SearchKeywordRepository repository)
    {
        _repository = repository;
    }

    public async Task<SearchKeywordListResult> ListKeywordsAsync(
        string? search = null,
        string? targetType = null,
        string? category = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var items = await _repository.SearchAsync(search, targetType, category, isActive, ct);
        var records = items.Select(k => new SearchKeywordRecord(
            k.Id,
            k.Phrase,
            k.TargetType,
            k.Category,
            k.TargetValue,
            k.IsActive,
            0
        )).ToList();

        return new SearchKeywordListResult(records, records.Count);
    }

    public SearchKeywordOptionsResult GetOptions()
    {
        var targetTypes = new List<SearchKeywordOptionDto>
        {
            new(1, "Order", "Order", "TargetType"),
            new(2, "Customer", "Customer", "TargetType"),
            new(3, "Product", "Product", "TargetType"),
            new(4, "Courier", "Courier", "TargetType")
        };

        var categories = new List<SearchKeywordOptionDto>
        {
            new(1, "General", "General", "Category"),
            new(2, "FollowUp", "FollowUp", "Category"),
            new(3, "Complaints", "Complaints", "Category"),
            new(4, "Investigation", "Investigation", "Category")
        };

        return new SearchKeywordOptionsResult(targetTypes, categories);
    }

    public async Task<SearchKeywordRecord> CreateKeywordAsync(CreateSearchKeywordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Keyword))
        {
            throw new BadRequestException("Keyword cannot be empty.");
        }

        var entity = new SearchKeywordOption
        {
            Phrase = request.Keyword.Trim(),
            NormalizedPhrase = NormalizePhrase(request.Keyword),
            TargetType = request.TargetType?.Trim() ?? "OrderStatus",
            Category = request.Category?.Trim() ?? "عام",
            TargetValue = request.TargetValue?.Trim() ?? string.Empty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.AddAsync(entity, ct);
        return new SearchKeywordRecord(
            created.Id,
            created.Phrase,
            created.TargetType,
            created.Category,
            created.TargetValue,
            created.IsActive,
            0
        );
    }

    private static string NormalizePhrase(string value) =>
        value.Trim().ToLowerInvariant()
            .Replace('أ', 'ا')
            .Replace('إ', 'ا')
            .Replace('آ', 'ا')
            .Replace('ة', 'ه')
            .Replace('ى', 'ي')
            .Replace('ؤ', 'و')
            .Replace('ئ', 'ي')
            .Replace("ـ", string.Empty)
            .Replace("  ", " ");
}
