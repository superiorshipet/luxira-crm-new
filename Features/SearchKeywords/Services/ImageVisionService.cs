using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Luxira.Api.Features.SearchKeywords.Services;

public sealed class ImageVisionService
{
    private readonly IHttpClientFactory _clients;
    private readonly IConfiguration _configuration;

    public ImageVisionService(IHttpClientFactory clients, IConfiguration configuration)
    {
        _clients = clients;
        _configuration = configuration;
    }

    public async Task<VisionSearchResult> ExtractAsync(IFormFile image, CancellationToken ct)
    {
        var apiKey = _configuration["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return new(null, "Groq API key is not configured.");

        await using var stream = image.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        var mimeType = string.IsNullOrWhiteSpace(image.ContentType) ? "image/jpeg" : image.ContentType;
        var payload = new
        {
            model = _configuration["Groq:VisionModel"] ?? "llama-3.2-11b-vision-preview",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = "Copy only text literally visible in the image. Prefer a phone number or order ID, otherwise a customer name. Return digits without spaces. If uncertain return NONE. One line only; no explanation." },
                        new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{Convert.ToBase64String(memory.ToArray())}" } }
                    }
                }
            },
            temperature = 0,
            max_tokens = 64
        };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{(_configuration["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1").TrimEnd('/')}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _clients.CreateClient().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return new(null, $"Image OCR failed with status {(int)response.StatusCode}.");

        var body = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(body);
        var content = document.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 &&
                      choices[0].TryGetProperty("message", out var message) &&
                      message.TryGetProperty("content", out var value)
            ? value.GetString()
            : null;
        return new(Normalize(content), null);
    }

    public static string? Normalize(string? content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Contains("NONE", StringComparison.OrdinalIgnoreCase)) return null;
        var thinkStart = content.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        if (thinkStart >= 0 && content.IndexOf("</think>", thinkStart, StringComparison.OrdinalIgnoreCase) < 0) return null;
        content = Regex.Replace(content, @"<think>[\s\S]*?</think>", string.Empty, RegexOptions.IgnoreCase).Trim();
        content = content.Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
            .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7').Replace('٨', '8').Replace('٩', '9');
        var number = Regex.Matches(content, @"(?<!\d)\+?[\d][\d\s\-()]{1,}[\d](?!\d)")
            .Select(match => Regex.Replace(match.Value, @"\D", string.Empty))
            .Where(value => value.Length >= 3)
            .OrderByDescending(value => value.Length)
            .FirstOrDefault();
        if (number is not null) return number;
        return content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Trim('`', '"', '\'', '.', ':', '-', '*'))
            .FirstOrDefault(line => line.Length > 0);
    }
}

public sealed record VisionSearchResult(string? Query, string? Error);
