using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using PawConnect.Core.Enums;
using PawConnect.Core.Notifications;

namespace PawConnect.Infrastructure.Notifications;

/// <summary>
/// Development stand-in for the SMS channel (Notifications:Sms:Provider = "Log"): the text message
/// is written to the application log instead of being sent. Production uses TwilioSmsStrategy.
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
