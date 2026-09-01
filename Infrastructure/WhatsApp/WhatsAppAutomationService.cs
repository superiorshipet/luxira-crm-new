using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Luxira.Api.Data;
using Luxira.Api.Utils.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Luxira.Api.Infrastructure.WhatsApp;

public class WhatsAppAutomationService
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WhatsAppAutomationService> _logger;
    private readonly bool _enabled;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _sender;
    private readonly string _templateName;

    public WhatsAppAutomationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<WhatsAppAutomationService> logger)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClient;

        _enabled = configuration.GetValue<bool?>("WhatsApp:Infobip:Enabled") ?? false;
        _baseUrl = configuration["WhatsApp:Infobip:BaseUrl"] ?? "qw48j2.api.infobip.com";
        _apiKey = configuration["WhatsApp:Infobip:ApiKey"] ?? string.Empty;
        _sender = configuration["WhatsApp:Infobip:Sender"] ?? "447860088970";
        _templateName = configuration["WhatsApp:Infobip:TemplateName"] ?? "test_whatsapp_template_en";
    }

    public async Task<bool> SendOrderAlertAsync(int orderId, string recipientPhone, string messageText, CancellationToken ct = default)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogInformation("WhatsApp Infobip automation disabled or missing API key.");
            return false;
        }

        try
        {
            var url = $"https://{_baseUrl}/whatsapp/1/message/text";
            var payload = new
            {
                from = _sender,
                to = recipientPhone.TrimStart('+').Trim(),
                content = new { text = messageText }
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, url);
            message.Headers.Add("Authorization", $"App {_apiKey}");
            message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(message, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp automation alert for Order {OrderId}", orderId);
            return false;
        }
    }
}
