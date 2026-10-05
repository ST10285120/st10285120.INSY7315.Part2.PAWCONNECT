using PawConnect.Core.Enums;

namespace PawConnect.Core.Notifications;

/// <summary>
/// Picks strategies based on cost and urgency (design doc, section 9, "Cost Optimization"):
/// email is the default because it is nearly free. SMS is only used for high-urgency
/// messages such as shift reminders, and email is kept as the fallback if SMS fails.
/// </summary>
public class NotificationStrategySelector
{
    private readonly IReadOnlyList<INotificationStrategy> _strategies;

    public NotificationStrategySelector(IEnumerable<INotificationStrategy> strategies)
    {
        _strategies = strategies.ToList();
    }

    /// <summary>Strategies to try, in order. Later entries are fallbacks.</summary>
    public IReadOnlyList<INotificationStrategy> SelectFor(NotificationRequest request)
    {
        var preferredOrder = request.Urgency == NotificationUrgency.High
            ? new[] { NotificationChannel.Sms, NotificationChannel.Email }
            : new[] { NotificationChannel.Email };

        return preferredOrder
            .SelectMany(channel => _strategies.Where(s => s.Channel == channel))
            .Where(s => s.CanSend(request))
            .ToList();
    }
}
