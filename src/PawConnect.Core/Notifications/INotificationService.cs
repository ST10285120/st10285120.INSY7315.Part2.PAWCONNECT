using PawConnect.Core.Enums;

namespace PawConnect.Core.Notifications;

public interface INotificationService
{
    /// <summary>Sends a notification. Never throws: a failed email must not undo the business action.</summary>
    Task SendAsync(NotificationRequest request, CancellationToken ct = default);
}
