using PawConnect.Core.Enums;

namespace PawConnect.Core.Notifications;

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
