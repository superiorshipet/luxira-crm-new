using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Luxira.Api.Infrastructure.WhatsApp;

public sealed class LavvaFailedDeliveryWhatsAppRequest
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerCountry { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
}

public sealed class LavvaWhatsAppSendResult
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string SenderNumber { get; set; } = string.Empty;
    public string RecipientNumber { get; set; } = string.Empty;
    public string? MessageId { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class LavvaWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LavvaWhatsAppService> _logger;
    private readonly bool _enabled;
    private readonly string _senderNumber;
    private readonly string _phoneNumberId;
    private readonly string _accessToken;
    private readonly string _apiVersion;
    private readonly string _templateName;
    private readonly string _templateLanguageCode;

    public LavvaWhatsAppService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LavvaWhatsAppService> logger)
    {
        _logger = logger;
        _httpClient = httpClient;

        _enabled = configuration.GetValue<bool?>("WhatsApp:Lavva:Enabled") ?? false;
        _senderNumber = configuration["WhatsApp:Lavva:SenderNumber"] ?? "+905377144098";
        _phoneNumberId = configuration["WhatsApp:Lavva:PhoneNumberId"] ?? string.Empty;
        _accessToken = configuration["WhatsApp:Lavva:AccessToken"] ?? string.Empty;
        _apiVersion = configuration["WhatsApp:Lavva:ApiVersion"] ?? "v23.0";
        _templateName = configuration["WhatsApp:Lavva:TemplateName"] ?? "lavva_failed_delivery_follow_up";
        _templateLanguageCode = configuration["WhatsApp:Lavva:TemplateLanguageCode"] ?? "ar";
    }

    public async Task<LavvaWhatsAppSendResult> SendFailedDeliveryAsync(
        LavvaFailedDeliveryWhatsAppRequest request,
        CancellationToken ct = default)
    {
        var result = new LavvaWhatsAppSendResult { SenderNumber = _senderNumber };

        if (!_enabled)
        {
            result.Skipped = true;
            result.ErrorMessage = "Lavva WhatsApp integration is disabled in configuration.";
            return result;
        }

        if (string.IsNullOrWhiteSpace(_phoneNumberId) || string.IsNullOrWhiteSpace(_accessToken))
        {
            result.Skipped = true;
            result.ErrorMessage = "Lavva WhatsApp credentials (PhoneNumberId/AccessToken) are missing.";
            return result;
        }

        var normalizedPhone = NormalizePhoneNumber(request.CustomerPhone);
        result.RecipientNumber = normalizedPhone;

        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            result.ErrorMessage = "Invalid phone number.";
            return result;
        }

        try
        {
            var url = $"https://graph.facebook.com/{_apiVersion}/{_phoneNumberId}/messages";

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = normalizedPhone,
                type = "template",
                template = new
                {
                    name = _templateName,
                    language = new { code = _templateLanguageCode },
                    components = new object[]
                    {
                        new
                        {
                            type = "body",
                            parameters = new object[]
                            {
                                new { type = "text", text = string.IsNullOrWhiteSpace(request.CustomerName) ? "عميلنا العزيز" : request.CustomerName.Trim() },
                                new { type = "text", text = request.OrderId.ToString() },
                                new { type = "text", text = string.IsNullOrWhiteSpace(request.StoreName) ? "LAVVA" : request.StoreName.Trim() }
                            }
                        }
                    }
                }
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, url);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(message, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                result.Success = true;
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("messages", out var messagesEl) && messagesEl.GetArrayLength() > 0)
                {
                    result.MessageId = messagesEl[0].GetProperty("id").GetString();
                }
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "WhatsApp template sent to {Phone} for Order {OrderId}",
                        normalizedPhone,
                        request.OrderId);
                }
            }
            else
            {
                result.ErrorMessage = $"Meta API error: {response.StatusCode} - {content}";
                _logger.LogError("WhatsApp send failed for Order {OrderId}: {Error}", request.OrderId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Exception sending WhatsApp for Order {OrderId}", request.OrderId);
        }

        return result;
    }

    private static string NormalizePhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.TrimStart('0');
    }
}
