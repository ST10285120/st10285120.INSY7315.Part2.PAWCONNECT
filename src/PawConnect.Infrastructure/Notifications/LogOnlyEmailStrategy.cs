using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using PawConnect.Core.Enums;
using PawConnect.Core.Notifications;

namespace PawConnect.Infrastructure.Notifications;

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
