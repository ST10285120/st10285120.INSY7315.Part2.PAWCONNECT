using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using PawConnect.Core.Enums;
using PawConnect.Core.Notifications;

namespace PawConnect.Infrastructure.Notifications;

public class EmailOptions
{
    /// <summary>SMTP host, e.g. smtp.azurecomm.net (Azure Communication Services) or smtp.sendgrid.net.</summary>
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = "no-reply@pawconnect.local";
    public string FromName { get; set; } = "Hope & Paws Animal Shelter";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpHost);
}

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

/// <summary>
/// Development stand-in used when no SMTP server is configured: the email is written to the
/// application log (and the Notifications table) instead of being sent.
/// </summary>
public class LogOnlyEmailStrategy : INotificationStrategy
{
    private readonly ILogger<LogOnlyEmailStrategy> _logger;
    public LogOnlyEmailStrategy(ILogger<LogOnlyEmailStrategy> logger) => _logger = logger;

    public NotificationChannel Channel => NotificationChannel.Email;
    public bool CanSend(NotificationRequest request) => !string.IsNullOrWhiteSpace(request.Email);
    public string RecipientFor(NotificationRequest request) => request.Email ?? "";

    public Task SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("[email not sent - SMTP not configured] To: {To} | Subject: {Subject}", request.Email, request.Subject);
        return Task.CompletedTask;
    }
}

/// <summary>
/// SMS channel placeholder. It is registered only when Notifications:Sms:Enabled is true, and it
/// logs the text message. To go live, implement INotificationStrategy against an SMS provider
/// (e.g. Azure Communication Services SMS) and register it instead. No other code changes are needed.
/// </summary>
public class LogOnlySmsStrategy : INotificationStrategy
{
    private readonly ILogger<LogOnlySmsStrategy> _logger;
    public LogOnlySmsStrategy(ILogger<LogOnlySmsStrategy> logger) => _logger = logger;

    public NotificationChannel Channel => NotificationChannel.Sms;
    public bool CanSend(NotificationRequest request) => !string.IsNullOrWhiteSpace(request.Phone);
    public string RecipientFor(NotificationRequest request) => request.Phone ?? "";

    public Task SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("[sms] To: {Phone} | {Subject}", request.Phone, request.Subject);
        return Task.CompletedTask;
    }
}
