using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(NotificationMessage message, CancellationToken ct = default);
    Task<List<NotificationMessage>> GetRecentAsync(int count, CancellationToken ct = default);
}
