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
        var result = await _service.ListKeywordsAsync(search: request.TranscribedText, ct: ct);
        return Ok(result);
    }
}

public record VoiceQueryRequest(string TranscribedText, string? Language);
