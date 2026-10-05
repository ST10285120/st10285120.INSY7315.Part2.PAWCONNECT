using PawConnect.Core.Enums;

namespace PawConnect.Core.Notifications;

/// <summary>What to send and to whom. The strategy decides how it is delivered.</summary>
public record NotificationRequest(
    string Subject,
    string Body,
    string? Email,
    string? Phone = null,
    Guid? RecipientUserId = null,
    NotificationUrgency Urgency = NotificationUrgency.Normal);

/// <summary>
/// Strategy pattern (design doc, section 6.1): each delivery channel is an interchangeable
/// strategy behind the same SendAsync method. A new channel (e.g. WhatsApp) can be added by
/// registering another strategy, without changing the services that send notifications.
/// </summary>
public interface INotificationStrategy
{
    NotificationChannel Channel { get; }

    /// <summary>True if this channel is configured and the request has the details it needs.</summary>
    bool CanSend(NotificationRequest request);

    Task SendAsync(NotificationRequest request, CancellationToken ct = default);

    /// <summary>The address actually used (email address or phone number) for the audit log.</summary>
    string RecipientFor(NotificationRequest request);
}

public interface INotificationService
{
    /// <summary>Sends a notification. Never throws: a failed email must not undo the business action.</summary>
    Task SendAsync(NotificationRequest request, CancellationToken ct = default);
}
