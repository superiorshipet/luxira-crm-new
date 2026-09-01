using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Luxira.Api.Infrastructure.Email;

public class LuxiraEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LuxiraEmailService> _logger;
    private readonly bool _enabled;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _appPassword;
    private readonly string _defaultToEmail;

    public LuxiraEmailService(IConfiguration configuration, ILogger<LuxiraEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        _enabled = configuration.GetValue<bool?>("LuxiraMail:Enabled") ?? true;
        _smtpHost = configuration["LuxiraMail:SmtpHost"] ?? "smtp.gmail.com";
        _smtpPort = configuration.GetValue<int?>("LuxiraMail:SmtpPort") ?? 587;
        _fromEmail = configuration["LuxiraMail:FromEmail"] ?? "holdingluxira@gmail.com";
        _fromName = configuration["LuxiraMail:FromName"] ?? "Luxira CRM";
        _appPassword = configuration["LuxiraMail:AppPassword"] ?? string.Empty;
        _defaultToEmail = configuration["LuxiraMail:DefaultToEmail"] ?? "luxiraholding@gmail.com";
    }

    public async Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string bodyHtml,
        byte[]? attachmentBytes = null,
        string? attachmentFileName = null,
        CancellationToken ct = default)
    {
        if (!_enabled)
        {
            _logger.LogInformation("LuxiraMail is disabled. Skipped sending email to {To}", toEmail);
            return false;
        }

        try
        {
            var targetTo = string.IsNullOrWhiteSpace(toEmail) ? _defaultToEmail : toEmail;

            using var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };

            message.To.Add(targetTo);

            if (attachmentBytes != null && !string.IsNullOrWhiteSpace(attachmentFileName))
            {
                var ms = new MemoryStream(attachmentBytes);
                message.Attachments.Add(new Attachment(ms, attachmentFileName));
            }

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_fromEmail, _appPassword)
            };

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Email sent successfully to {To} with subject: {Subject}", targetTo, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", toEmail);
            return false;
        }
    }
}
