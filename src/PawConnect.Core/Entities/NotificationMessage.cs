using PawConnect.Core.Enums;

namespace PawConnect.Core.Entities;

/// <summary>Log of every notification PawConnect sent (or failed to send), for support and audit.</summary>
public class NotificationMessage
{
    public Guid Id { get; set; }
    public Guid? RecipientUserId { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public NotificationUrgency Urgency { get; set; }
    public NotificationStatus Status { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
}
