using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using PawConnect.Core.Enums;
using PawConnect.Core.Notifications;

namespace PawConnect.Infrastructure.Notifications;

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
