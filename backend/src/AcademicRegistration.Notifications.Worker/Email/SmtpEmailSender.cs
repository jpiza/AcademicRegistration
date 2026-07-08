using System.Net;
using System.Net.Mail;
using AcademicRegistration.Notifications.Worker.Configuration;
using Microsoft.Extensions.Options;

namespace AcademicRegistration.Notifications.Worker.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation(
                "Correo simulado. To: {To}, Subject: {Subject}, Body: {Body}",
                message.To,
                message.Subject,
                message.Body);

            return;
        }

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_settings.From, _settings.FromName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false
        };

        mailMessage.To.Add(message.To);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            UseDefaultCredentials = false
        };

        if (_settings.RequireAuthentication || !string.IsNullOrWhiteSpace(_settings.UserName))
        {
            client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
        }

        await client.SendMailAsync(mailMessage, cancellationToken);
    }
}
