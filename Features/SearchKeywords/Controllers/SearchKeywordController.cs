using Luxira.Api.Features.SearchKeywords.DTOs;
using Luxira.Api.Features.SearchKeywords.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.SearchKeywords.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/administration/search-keywords")]
[Route("api/[controller]")]
public class SearchKeywordController : ControllerBase
{
    private readonly SearchKeywordService _service;

    public SearchKeywordController(SearchKeywordService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("/Home/GetSearchKeywords")]
    public async Task<ActionResult<SearchKeywordListResult>> ListKeywords(
        [FromQuery] string? search,
        [FromQuery] string? targetType,
        [FromQuery] string? category,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var result = await _service.ListKeywordsAsync(search, targetType, category, isActive, ct);
        return Ok(result);
    }

    [Authorize(Roles = "Admin,Administrator")]
    [HttpPost]
    public async Task<ActionResult<SearchKeywordRecord>> CreateKeyword(
        [FromBody] CreateSearchKeywordRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateKeywordAsync(request, ct);
        return Ok(result);
    }
}
