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
