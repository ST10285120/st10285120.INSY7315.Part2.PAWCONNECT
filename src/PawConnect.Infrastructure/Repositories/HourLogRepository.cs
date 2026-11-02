using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Infrastructure.Repositories;

public class HourLogRepository : IHourLogRepository
{
    private readonly AppDbContext _db;
    public HourLogRepository(AppDbContext db) => _db = db;

    public Task<HourLog?> GetAsync(Guid id, CancellationToken ct = default) => _db.HourLogs.FirstOrDefaultAsync(h => h.Id == id, ct);

    public Task<List<HourLog>> FindAsync(HourLogFilter filter, CancellationToken ct = default)
    {
        var q = _db.HourLogs.AsNoTracking().Include(h => h.Volunteer).AsQueryable();
        if (filter.VolunteerId is { } v) q = q.Where(h => h.VolunteerId == v);
        if (filter.Status is { } s) q = q.Where(h => h.Status == s);
        return q.OrderByDescending(h => h.Date).ThenByDescending(h => h.LoggedAt).ToListAsync(ct);
    }

    public async Task<decimal> SumApprovedAsync(Guid? volunteerId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var q = _db.HourLogs.Where(h => h.Status == HourLogStatus.Approved);
        if (volunteerId is { } v) q = q.Where(h => h.VolunteerId == v);
        if (from is { } f) q = q.Where(h => h.Date >= f);
        if (to is { } t) q = q.Where(h => h.Date <= t);
        return await q.SumAsync(h => (decimal?)h.Hours, ct) ?? 0m;
    }

    public async Task<Dictionary<Guid, decimal>> ApprovedTotalsByVolunteerAsync(CancellationToken ct = default)
    {
        var rows = await _db.HourLogs.Where(h => h.Status == HourLogStatus.Approved)
            .GroupBy(h => h.VolunteerId)
            .Select(g => new { VolunteerId = g.Key, Total = g.Sum(h => h.Hours) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.VolunteerId, r => r.Total);
    }

    public Task<int> CountPendingAsync(Guid? volunteerId = null, CancellationToken ct = default) =>
        _db.HourLogs.CountAsync(h => h.Status == HourLogStatus.Pending && (volunteerId == null || h.VolunteerId == volunteerId), ct);

    public async Task AddAsync(HourLog log, CancellationToken ct = default) => await _db.HourLogs.AddAsync(log, ct);
}
