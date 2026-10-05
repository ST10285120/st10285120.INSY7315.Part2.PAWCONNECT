using Microsoft.Extensions.Logging;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Notifications;

namespace PawConnect.Core.Services;

/// <summary>
/// Volunteer shift roster. It replaces the WhatsApp group, and its success criterion is
/// "zero double bookings". Three rules enforce that:
/// 1. a shift can't go over capacity, even when two volunteers sign up at the same moment
///    (optimistic concurrency on Shift.ConcurrencyStamp, plus a retry);
/// 2. a volunteer can't sign up for the same shift twice (checked here and by a unique index);
/// 3. a volunteer can't sign up for two shifts that overlap in time.
/// </summary>
public class ShiftService
{
    private const int MaxRetries = 3;

    private readonly IShiftRepository _shifts;
    private readonly IUserRepository _users;
    private readonly IBranchRepository _branches;
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly IClock _clock;
    private readonly ILogger<ShiftService> _logger;

    public ShiftService(IShiftRepository shifts, IUserRepository users, IBranchRepository branches, IUnitOfWork uow,
        INotificationService notifications, IClock clock, ILogger<ShiftService> logger)
    {
        _shifts = shifts;
        _users = users;
        _branches = branches;
        _uow = uow;
        _notifications = notifications;
        _clock = clock;
        _logger = logger;
    }

    public async Task<OperationResult> SignUpAsync(Guid shiftId, Guid volunteerId, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var shift = await _shifts.GetAsync(shiftId, ct);
            if (shift == null) return OperationResult.NotFound("That shift no longer exists.");
            // Volunteers work at their own branch; a volunteer not yet assigned to one can join any shift.
            if (attempt == 1 && await _users.GetAsync(volunteerId, ct) is { BranchId: { } branchId } && branchId != shift.BranchId)
                return OperationResult.NotFound("That shift no longer exists.");
            if (shift.StartsAt <= _clock.UtcNow) return OperationResult.Conflict("That shift has already started.");
            if (shift.Signups.Any(s => s.VolunteerId == volunteerId)) return OperationResult.Conflict("You're already signed up for this shift.");
            if (shift.IsFull) return OperationResult.Conflict("Sorry, that shift is already full.");
            if (await _shifts.HasOverlappingSignupAsync(volunteerId, shift.StartsAt, shift.EndsAt, ct))
                return OperationResult.Conflict("You're already booked on another shift at that time.");

            await _shifts.AddSignupAsync(new ShiftSignup
            {
                Id = Guid.NewGuid(), ShiftId = shift.Id, VolunteerId = volunteerId, SignedUpAt = _clock.UtcNow
            }, ct);
            shift.ConcurrencyStamp = Guid.NewGuid(); // makes a concurrent sign-up for this shift fail and retry

            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxRetries)
            {
                _logger.LogInformation("Concurrent sign-up on shift {ShiftId}, retrying (attempt {Attempt})", shiftId, attempt);
                _uow.DiscardChanges();
                continue;
            }
            catch (ConcurrencyConflictException)
            {
                _uow.DiscardChanges();
                return OperationResult.Conflict("That shift is very busy right now. Please try again.");
            }

            var volunteer = await _users.GetAsync(volunteerId, ct);
            if (volunteer != null)
                await _notifications.SendAsync(NotificationTemplates.ShiftConfirmed(volunteer, shift), ct);
            return OperationResult.Success();
        }
    }

    public async Task<OperationResult> CancelAsync(Guid shiftId, Guid volunteerId, CancellationToken ct = default)
    {
        var shift = await _shifts.GetAsync(shiftId, ct);
        var signup = shift?.Signups.FirstOrDefault(s => s.VolunteerId == volunteerId);
        if (shift == null || signup == null) return OperationResult.NotFound("You're not signed up for that shift.");
        if (shift.StartsAt <= _clock.UtcNow) return OperationResult.Conflict("You can't cancel a shift that has already started.");

        _shifts.RemoveSignup(signup);
        shift.ConcurrencyStamp = Guid.NewGuid();
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            _uow.DiscardChanges();
            return OperationResult.Conflict("The shift changed while you were cancelling. Please try again.");
        }
        return OperationResult.Success();
    }

    public async Task<OperationResult<Shift>> CreateAsync(ShiftInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Role) || input.Role.Length > 60) return OperationResult<Shift>.Failure("Please give the shift a role, e.g. Dog walking.");
        if (input.Capacity is < 1 or > 50) return OperationResult<Shift>.Failure("Capacity must be between 1 and 50.");

        var startsAt = ShelterTime.ToUtc(input.StartsAtLocal);
        var endsAt = ShelterTime.ToUtc(input.EndsAtLocal);
        if (endsAt <= startsAt) return OperationResult<Shift>.Failure("The shift must end after it starts.");
        if (endsAt - startsAt > TimeSpan.FromHours(12)) return OperationResult<Shift>.Failure("Shifts can be at most 12 hours long.");
        if (startsAt <= _clock.UtcNow) return OperationResult<Shift>.Failure("Shifts must be scheduled in the future.");

        var branchId = input.BranchId ?? (await _branches.GetDefaultAsync(ct)).Id;
        var shift = new Shift
        {
            Id = Guid.NewGuid(), BranchId = branchId, StartsAt = startsAt, EndsAt = endsAt,
            Role = input.Role.Trim(), Capacity = input.Capacity,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
        };
        await _shifts.AddAsync(shift, ct);
        await _uow.SaveChangesAsync(ct);
        return OperationResult<Shift>.Success(shift);
    }
}
