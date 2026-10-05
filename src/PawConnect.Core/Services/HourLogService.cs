using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;

namespace PawConnect.Core.Services;

/// <summary>Volunteers log hours; an administrator approves them before they count in reports.</summary>
public class HourLogService
{
    public const int MaxDaysBack = 90;

    private readonly IHourLogRepository _logs;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public HourLogService(IHourLogRepository logs, IUnitOfWork uow, IClock clock)
    {
        _logs = logs;
        _uow = uow;
        _clock = clock;
    }

    public async Task<OperationResult<HourLog>> LogAsync(Guid volunteerId, HourLogInput input, CancellationToken ct = default)
    {
        var today = ShelterTime.Today(_clock);
        if (input.Hours <= 0 || input.Hours > 24) return OperationResult<HourLog>.Failure("Hours must be more than 0 and at most 24.");
        if (input.Hours * 4 != decimal.Truncate(input.Hours * 4)) return OperationResult<HourLog>.Failure("Please log hours in quarter-hour steps (e.g. 2.5).");
        if (input.Date > today) return OperationResult<HourLog>.Failure("You can't log hours for a future date.");
        if (input.Date < today.AddDays(-MaxDaysBack)) return OperationResult<HourLog>.Failure($"Hours older than {MaxDaysBack} days can't be logged. Please speak to the shelter manager.");
        if (string.IsNullOrWhiteSpace(input.Activity) || input.Activity.Length > 60) return OperationResult<HourLog>.Failure("Please choose an activity.");
        if (input.Notes?.Length > 500) return OperationResult<HourLog>.Failure("Notes can be at most 500 characters.");

        // A person can't volunteer more than 24 hours on one day, across all their logs.
        var sameDay = await _logs.FindAsync(new HourLogFilter(volunteerId), ct);
        var alreadyLogged = sameDay.Where(l => l.Date == input.Date && l.Status != HourLogStatus.Rejected).Sum(l => l.Hours);
        if (alreadyLogged + input.Hours > 24)
            return OperationResult<HourLog>.Conflict($"You've already logged {alreadyLogged:0.##}h on that day; the total can't exceed 24h.");

        var log = new HourLog
        {
            Id = Guid.NewGuid(), VolunteerId = volunteerId, Date = input.Date, Hours = input.Hours,
            Activity = input.Activity.Trim(), Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            Status = HourLogStatus.Pending, LoggedAt = _clock.UtcNow
        };
        await _logs.AddAsync(log, ct);
        await _uow.SaveChangesAsync(ct);
        return OperationResult<HourLog>.Success(log);
    }

    public async Task<OperationResult> ReviewAsync(Guid logId, bool approve, Guid adminId, CancellationToken ct = default)
    {
        var log = await _logs.GetAsync(logId, ct);
        if (log == null) return OperationResult.NotFound("Hour log not found.");
        if (log.Status != HourLogStatus.Pending) return OperationResult.Conflict("This hour log has already been reviewed.");

        log.Status = approve ? HourLogStatus.Approved : HourLogStatus.Rejected;
        log.ReviewedById = adminId;
        log.ReviewedAt = _clock.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return OperationResult.Success();
    }
}
