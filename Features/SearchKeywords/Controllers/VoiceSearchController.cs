using Luxira.Api.Features.SearchKeywords.DTOs;
using Luxira.Api.Features.SearchKeywords.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.SearchKeywords.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/search/voice")]
[Route("VoiceSearch")]
public class VoiceSearchController : ControllerBase
{
    private readonly SearchKeywordService _service;

    public VoiceSearchController(SearchKeywordService service)
    {
        _service = service;
    }

    [HttpPost]
    [HttpPost("ProcessVoiceQuery")]
    public async Task<ActionResult<SearchKeywordListResult>> ProcessVoiceQuery([FromBody] VoiceQueryRequest request, CancellationToken ct)
    {
        // Search using transcribed voice text query
        var filter = new SearchKeywordFilterRequest(request.TranscribedText, null, 1, 20);
        var result = await _service.GetKeywordsAsync(filter, ct);
        return Ok(result);
    }
}

public record VoiceQueryRequest(string TranscribedText, string? Language);
