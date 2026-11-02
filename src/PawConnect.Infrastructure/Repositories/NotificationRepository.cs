using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;
    public NotificationRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(NotificationMessage message, CancellationToken ct = default)
    {
        // Keep the log row within the column limits even for unusually long messages.
        if (message.Body.Length > 4000) message.Body = message.Body[..4000];
        if (message.Subject.Length > 200) message.Subject = message.Subject[..200];
        if (message.Error?.Length > 1000) message.Error = message.Error[..1000];
        await _db.Notifications.AddAsync(message, ct);
    }

    public Task<List<NotificationMessage>> GetRecentAsync(int count, CancellationToken ct = default) =>
        _db.Notifications.AsNoTracking().OrderByDescending(n => n.CreatedAt).Take(count).ToListAsync(ct);
}
