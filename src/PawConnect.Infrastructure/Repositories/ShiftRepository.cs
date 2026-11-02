using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Infrastructure.Repositories;

public class ShiftRepository : IShiftRepository
{
    private readonly AppDbContext _db;
    public ShiftRepository(AppDbContext db) => _db = db;

    public Task<Shift?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Shifts.Include(s => s.Signups).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<Shift>> GetUpcomingAsync(DateTime fromUtc, Guid? branchId, CancellationToken ct = default) =>
        _db.Shifts.AsNoTracking()
            .Include(s => s.Signups).ThenInclude(x => x.Volunteer)
            .Where(s => s.EndsAt >= fromUtc && (branchId == null || s.BranchId == branchId))
            .OrderBy(s => s.StartsAt)
            .ToListAsync(ct);

    public Task<List<ShiftSignup>> GetSignupsForVolunteerAsync(Guid volunteerId, DateTime fromUtc, CancellationToken ct = default) =>
        _db.ShiftSignups.AsNoTracking()
            .Include(x => x.Shift)
            .Where(x => x.VolunteerId == volunteerId && x.Shift!.EndsAt >= fromUtc)
            .OrderBy(x => x.Shift!.StartsAt)
            .ToListAsync(ct);

    public Task<bool> HasOverlappingSignupAsync(Guid volunteerId, DateTime startsAt, DateTime endsAt, CancellationToken ct = default) =>
        // Two intervals overlap when each starts before the other ends.
        _db.ShiftSignups.AnyAsync(x => x.VolunteerId == volunteerId
                                       && x.Shift!.StartsAt < endsAt && startsAt < x.Shift.EndsAt, ct);

    public async Task AddAsync(Shift shift, CancellationToken ct = default) => await _db.Shifts.AddAsync(shift, ct);
    public async Task AddSignupAsync(ShiftSignup signup, CancellationToken ct = default) => await _db.ShiftSignups.AddAsync(signup, ct);
    public void RemoveSignup(ShiftSignup signup) => _db.ShiftSignups.Remove(signup);
}
