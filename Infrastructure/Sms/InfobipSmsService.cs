using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Luxira.Api.Infrastructure.Sms;

public sealed class SmsSendRequest
{
    public string To { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class SmsSendResult
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string RecipientNumber { get; set; } = string.Empty;
    public string? MessageId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusDescription { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class InfobipSmsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InfobipSmsService> _logger;
    private readonly bool _enabled;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _sender;

    public InfobipSmsService(IConfiguration configuration, ILogger<InfobipSmsService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        _enabled = configuration.GetValue<bool?>("Sms:Infobip:Enabled") ?? false;
        _baseUrl = configuration["Sms:Infobip:BaseUrl"] ?? "qw48j2.api.infobip.com";
        _apiKey = configuration["Sms:Infobip:ApiKey"] ?? string.Empty;
        _sender = configuration["Sms:Infobip:Sender"] ?? "ServiceSMS";
    }

    public async Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken ct = default)
    {
        var result = new SmsSendResult { Sender = _sender };

        if (!_enabled)
        {
            result.Skipped = true;
            result.ErrorMessage = "SMS Infobip is disabled.";
            return result;
        }

        if (string.IsNullOrWhiteSpace(_baseUrl) || string.IsNullOrWhiteSpace(_apiKey))
        {
            result.Skipped = true;
            result.ErrorMessage = "SMS Infobip credentials missing.";
            return result;
        }

        var normalizedPhone = NormalizePhone(request.To);
        result.RecipientNumber = normalizedPhone;

        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            result.ErrorMessage = "Invalid recipient phone number.";
            return result;
        }

        try
        {
            var url = $"https://{_baseUrl}/sms/2/text/advanced";
            var payload = new
            {
                messages = new object[]
                {
                    new
                    {
                        destinations = new object[] { new { to = normalizedPhone } },
                        from = _sender,
                        text = request.Text
                    }
                }
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, url);
            message.Headers.Add("Authorization", $"App {_apiKey}");
            message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(message, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                result.Success = true;
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("messages", out var messagesEl) && messagesEl.GetArrayLength() > 0)
                {
                    var first = messagesEl[0];
                    if (first.TryGetProperty("messageId", out var mid))
                        result.MessageId = mid.GetString();
                    if (first.TryGetProperty("status", out var st) && st.TryGetProperty("name", out var sname))
                        result.StatusName = sname.GetString();
                }
                _logger.LogInformation("SMS sent to {Phone}", normalizedPhone);
            }
            else
            {
                result.ErrorMessage = $"Infobip API error: {response.StatusCode} - {content}";
                _logger.LogError("SMS send failed to {Phone}: {Error}", normalizedPhone, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Exception sending SMS to {Phone}", normalizedPhone);
        }

        return result;
    }

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.TrimStart('0');
    }
}
