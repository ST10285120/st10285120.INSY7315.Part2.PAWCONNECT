using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using PawConnect.Core.Enums;
using PawConnect.Core.Notifications;

namespace PawConnect.Infrastructure.Notifications;

/// <summary>Email strategy that sends through any SMTP relay (Azure Communication Services supports SMTP).</summary>
public class SmtpEmailStrategy : INotificationStrategy
{
    private readonly EmailOptions _options;
    public SmtpEmailStrategy(EmailOptions options) => _options = options;

    public NotificationChannel Channel => NotificationChannel.Email;
    public bool CanSend(NotificationRequest request) => _options.IsConfigured && !string.IsNullOrWhiteSpace(request.Email);
    public string RecipientFor(NotificationRequest request) => request.Email ?? "";

    public async Task SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = request.Subject,
            Body = request.Body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(request.Email!));

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort) { EnableSsl = _options.EnableSsl };
        if (!string.IsNullOrEmpty(_options.UserName))
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        // A slow mail server must not hold the user's request for long: give each attempt 10 s.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await client.SendMailAsync(message, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("The mail server did not respond within 10 seconds.");
        }
    }
}
