using Luxira.Api.Features.SearchKeywords.DTOs;
using Luxira.Api.Features.SearchKeywords.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Luxira.Api.Features.SearchKeywords.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/search/voice")]
[Route("VoiceSearch")]
public class VoiceSearchController : ControllerBase
{
    private readonly SearchKeywordService _service;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public VoiceSearchController(
        SearchKeywordService service,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _service = service;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpPost]
    [HttpPost("ProcessVoiceQuery")]
    public async Task<ActionResult<SearchKeywordListResult>> ProcessVoiceQuery([FromBody] VoiceQueryRequest request, CancellationToken ct)
    {
        // Search using transcribed voice text query
        var result = await _service.ListKeywordsAsync(search: request.TranscribedText, ct: ct);
        return Ok(result);
    }

    [HttpPost("Transcribe")]
    [RequestSizeLimit(26L * 1024L * 1024L)]
    public async Task<IActionResult> Transcribe([FromForm] IFormFile? audio, CancellationToken ct)
    {
        audio ??= Request.HasFormContentType ? Request.Form.Files["file"] : null;
        if (audio is null || audio.Length == 0) return BadRequest(new { success = false, message = "ملف الصوت مطلوب." });
        if (audio.Length > 25L * 1024L * 1024L) return BadRequest(new { success = false, message = "حجم ملف الصوت يجب ألا يتجاوز 25 ميجابايت." });
        string[] allowed = [".flac", ".mp3", ".mp4", ".mpeg", ".mpga", ".m4a", ".ogg", ".wav", ".webm"];
        if (!audio.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) && !allowed.Contains(Path.GetExtension(audio.FileName), StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "نوع ملف الصوت غير مدعوم." });
        var apiKey = _configuration["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return BadRequest(new { success = false, message = "Groq API key is not configured." });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{(_configuration["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1").TrimEnd('/')}/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        await using var stream = audio.OpenReadStream();
        using var content = new MultipartFormDataContent();
        using var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(audio.ContentType) ? "application/octet-stream" : audio.ContentType);
        content.Add(file, "file", Path.GetFileName(audio.FileName));
        content.Add(new StringContent(_configuration["Groq:AudioTranscriptionModel"] ?? "whisper-large-v3-turbo"), "model");
        content.Add(new StringContent("json"), "response_format");
        content.Add(new StringContent("المستخدم بينطق رقم أوردر أو كود منتج بس، مفيش كلام تاني في الجملة. اكتبي التفريغ بالعربية بدقة."), "prompt");
        var language = _configuration["Groq:VoiceSearchLanguage"];
        if (!string.IsNullOrWhiteSpace(language)) content.Add(new StringContent(language), "language");
        request.Content = content;
        using var response = await _httpClientFactory.CreateClient().SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, new { success = false, message = $"Audio transcription failed with status {(int)response.StatusCode}." });
        using var document = JsonDocument.Parse(body);
        var query = document.RootElement.TryGetProperty("text", out var text)
            ? SpokenArabicNumberNormalizer.ExtractOrderNumber(text.GetString())
            : null;
        return string.IsNullOrWhiteSpace(query)
            ? Ok(new { success = false, query = (string?)null, message = "معلش، الرقم مش واضح. جربي تقوليه تاني ببطء وبوضوح." })
            : Ok(new { success = true, query, text = query });
    }
}

public record VoiceQueryRequest(string TranscribedText, string? Language);
