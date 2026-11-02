using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

public interface IHourLogRepository
{
    Task<HourLog?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<HourLog>> FindAsync(HourLogFilter filter, CancellationToken ct = default);
    Task<decimal> SumApprovedAsync(Guid? volunteerId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<Dictionary<Guid, decimal>> ApprovedTotalsByVolunteerAsync(CancellationToken ct = default);
    Task<int> CountPendingAsync(Guid? volunteerId = null, CancellationToken ct = default);
    Task AddAsync(HourLog log, CancellationToken ct = default);
}
