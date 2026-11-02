using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

public interface IShiftRepository
{
    Task<Shift?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<Shift>> GetUpcomingAsync(DateTime fromUtc, Guid? branchId, CancellationToken ct = default);
    Task<List<ShiftSignup>> GetSignupsForVolunteerAsync(Guid volunteerId, DateTime fromUtc, CancellationToken ct = default);
    Task<bool> HasOverlappingSignupAsync(Guid volunteerId, DateTime startsAt, DateTime endsAt, CancellationToken ct = default);
    Task AddAsync(Shift shift, CancellationToken ct = default);
    Task AddSignupAsync(ShiftSignup signup, CancellationToken ct = default);
    void RemoveSignup(ShiftSignup signup);
}
