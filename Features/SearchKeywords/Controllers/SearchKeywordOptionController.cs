using Luxira.Api.Features.SearchKeywords.DTOs;
using Luxira.Api.Features.SearchKeywords.Services;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.SearchKeywords.Controllers;

[ApiController]
[Route("api/v1/administration/search-keyword-options")]
[Route("api/[controller]")]
public class SearchKeywordOptionController : ControllerBase
{
    private readonly SearchKeywordService _service;

    public SearchKeywordOptionController(SearchKeywordService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("/Home/GetSearchKeywordOptions")]
    public ActionResult<SearchKeywordOptionsResult> GetOptions()
    {
        var result = _service.GetOptions();
        return Ok(result);
    }
}
