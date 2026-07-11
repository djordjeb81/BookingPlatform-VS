using System.Net;
using System.Net.Mail;
using BookingPlatform.Api.Options;
using Microsoft.Extensions.Options;

namespace BookingPlatform.Api.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new InvalidOperationException("Email primaoca nije podešen.");

        if (string.IsNullOrWhiteSpace(_options.Host))
            throw new InvalidOperationException("SMTP host nije podešen.");

        if (string.IsNullOrWhiteSpace(_options.Username))
            throw new InvalidOperationException("SMTP username nije podešen.");

        if (string.IsNullOrWhiteSpace(_options.Password))
            throw new InvalidOperationException("SMTP password nije podešen.");

        var fromEmail = string.IsNullOrWhiteSpace(_options.FromEmail)
            ? _options.Username
            : _options.FromEmail;

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        message.To.Add(toEmail);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            Credentials = new NetworkCredential(_options.Username, _options.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}