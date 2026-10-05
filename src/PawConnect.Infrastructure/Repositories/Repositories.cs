using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Infrastructure.Repositories;

// EF Core implementations of the Core repository interfaces. All queries go through LINQ,
// so EF Core sends parameterised SQL and user input is never concatenated into it (OWASP A03).
// Read-only queries use AsNoTracking for speed.

public class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public EfUnitOfWork(AppDbContext db) => _db = db;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The record was changed by someone else.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A unique index (e.g. one open application per animal) caught a race condition.
            throw new ConcurrencyConflictException("A conflicting record already exists.", ex);
        }
    }

    public void DiscardChanges() => _db.ChangeTracker.Clear();
}

public class AnimalRepository : IAnimalRepository
{
    private readonly AppDbContext _db;
    public AnimalRepository(AppDbContext db) => _db = db;

    public async Task<List<Animal>> SearchAsync(AnimalSearch s, CancellationToken ct = default)
    {
        var q = _db.Animals.AsNoTracking().AsQueryable();
        if (s.Species is { } species) q = q.Where(a => a.Species == species);
        if (s.Size is { } size) q = q.Where(a => a.Size == size);
        if (s.AvailableOnly) q = q.Where(a => a.Status == AnimalStatus.Available);
        else if (!s.IncludeAdopted) q = q.Where(a => a.Status != AnimalStatus.Adopted);
        if (s.GoodWithKids == true) q = q.Where(a => a.GoodWithKids);
        if (s.GoodWithOtherPets == true) q = q.Where(a => a.GoodWithOtherPets);
        if (s.BranchId is { } branchId) q = q.Where(a => a.BranchId == branchId);
        if (!string.IsNullOrWhiteSpace(s.Text))
        {
            var text = s.Text.Trim().ToLower();
            q = q.Where(a => a.Name.ToLower().Contains(text) || (a.Breed != null && a.Breed.ToLower().Contains(text)));
        }

        // Available animals first, then pending/fostered, adopted last; newest arrivals first within each.
        return await q
            .OrderBy(a => a.Status == AnimalStatus.Available ? 0 : a.Status == AnimalStatus.Adopted ? 2 : 1)
            .ThenByDescending(a => a.IntakeDate)
            .ThenBy(a => a.Name)
            .ToListAsync(ct);
    }

    public Task<Animal?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Animals.Include(a => a.Branch).FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Animal?> GetWithMedicalHistoryAsync(Guid id, CancellationToken ct = default) =>
        _db.Animals.AsNoTracking()
            .Include(a => a.MedicalRecords.OrderByDescending(m => m.RecordDate)).ThenInclude(m => m.RecordedBy)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<int> CountInCareAsync(CancellationToken ct = default) =>
        _db.Animals.CountAsync(a => a.Status != AnimalStatus.Adopted, ct);

    public Task<bool> KennelInUseAsync(Guid branchId, int kennelNumber, Guid? exceptAnimalId, CancellationToken ct = default) =>
        _db.Animals.AnyAsync(a => a.BranchId == branchId && a.KennelNumber == kennelNumber
                                  && a.Status != AnimalStatus.Adopted && a.Status != AnimalStatus.Fostered
                                  && a.Id != exceptAnimalId, ct);

    public async Task AddAsync(Animal animal, CancellationToken ct = default) => await _db.Animals.AddAsync(animal, ct);

    public async Task<Dictionary<Guid, Guid>> GetPrimaryPhotoIdsAsync(IEnumerable<Guid> animalIds, CancellationToken ct = default)
    {
        var ids = animalIds.Distinct().ToList();
        // Projection: only the ids are read, never the image bytes.
        var rows = await _db.AnimalPhotos.AsNoTracking()
            .Where(p => ids.Contains(p.AnimalId) && p.IsPrimary)
            .Select(p => new { p.AnimalId, p.Id })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.AnimalId).ToDictionary(g => g.Key, g => g.First().Id);
    }

    public Task<List<AnimalPhotoInfo>> GetPhotoInfosAsync(Guid animalId, CancellationToken ct = default) =>
        _db.AnimalPhotos.AsNoTracking()
            .Where(p => p.AnimalId == animalId)
            .OrderByDescending(p => p.IsPrimary).ThenBy(p => p.UploadedAt)
            .Select(p => new AnimalPhotoInfo(p.Id, p.AnimalId, p.IsPrimary, p.UploadedAt))
            .ToListAsync(ct);

    public Task<AnimalPhoto?> GetPhotoAsync(Guid photoId, CancellationToken ct = default) =>
        _db.AnimalPhotos.FirstOrDefaultAsync(p => p.Id == photoId, ct);

    public async Task AddPhotoAsync(AnimalPhoto photo, CancellationToken ct = default) => await _db.AnimalPhotos.AddAsync(photo, ct);

    public void RemovePhoto(AnimalPhoto photo) => _db.AnimalPhotos.Remove(photo);
}

public class ApplicationRepository : IApplicationRepository
{
    private static readonly ApplicationStatus[] OpenStatuses =
        { ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Approved };

    private readonly AppDbContext _db;
    public ApplicationRepository(AppDbContext db) => _db = db;

    public Task<AdoptionApplication?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.AdoptionApplications
            .Include(a => a.Animal)
            .Include(a => a.Adopter)
            .Include(a => a.ReviewedBy)
            .Include(a => a.StatusHistory.OrderBy(h => h.ChangedAt)).ThenInclude(h => h.ChangedBy)
            .Include(a => a.Donations)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<AdoptionApplication>> GetForAdopterAsync(Guid adopterId, CancellationToken ct = default) =>
        _db.AdoptionApplications.AsNoTracking()
            .Include(a => a.Animal)
            .Where(a => a.AdopterId == adopterId)
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync(ct);

    public Task<List<AdoptionApplication>> GetOpenForReviewAsync(Guid? branchId, CancellationToken ct = default) =>
        _db.AdoptionApplications.AsNoTracking()
            .Include(a => a.Animal)
            .Where(a => OpenStatuses.Contains(a.Status))
            .Where(a => branchId == null || a.Animal!.BranchId == branchId)
            .OrderBy(a => a.SubmittedAt)
            .ToListAsync(ct);

    public Task<List<AdoptionApplication>> GetRecentAsync(int count, CancellationToken ct = default) =>
        _db.AdoptionApplications.AsNoTracking()
            .Include(a => a.Animal)
            .OrderByDescending(a => a.SubmittedAt)
            .Take(count)
            .ToListAsync(ct);

    public Task<bool> HasActiveApplicationForAnimalAsync(Guid animalId, CancellationToken ct = default) =>
        _db.AdoptionApplications.AnyAsync(a => a.AnimalId == animalId && OpenStatuses.Contains(a.Status), ct);

    public Task<bool> ReferenceExistsAsync(string reference, CancellationToken ct = default) =>
        _db.AdoptionApplications.AnyAsync(a => a.ReferenceNumber == reference, ct);

    public Task<int> CountAdoptedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        _db.AdoptionApplications.CountAsync(a => a.Status == ApplicationStatus.Adopted
                                                 && a.CompletedAt >= fromUtc && a.CompletedAt < toUtc, ct);

    public Task<int> CountSubmittedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        _db.AdoptionApplications.CountAsync(a => a.SubmittedAt >= fromUtc && a.SubmittedAt < toUtc, ct);

    public async Task<double?> AverageDecisionHoursAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        // Success criterion: time from submission to a staff decision drops below 2 days.
        var pairs = await _db.AdoptionApplications.AsNoTracking()
            .Where(a => a.DecidedAt != null && a.SubmittedAt >= fromUtc && a.SubmittedAt < toUtc)
            .Select(a => new { a.SubmittedAt, a.DecidedAt })
            .ToListAsync(ct);
        return pairs.Count == 0 ? null : pairs.Average(p => (p.DecidedAt!.Value - p.SubmittedAt).TotalHours);
    }

    public async Task AddAsync(AdoptionApplication application, CancellationToken ct = default) =>
        await _db.AdoptionApplications.AddAsync(application, ct);
}

public class MedicalRecordRepository : IMedicalRecordRepository
{
    private readonly AppDbContext _db;
    public MedicalRecordRepository(AppDbContext db) => _db = db;

    public Task<List<MedicalRecord>> GetForAnimalAsync(Guid animalId, CancellationToken ct = default) =>
        _db.MedicalRecords.AsNoTracking()
            .Include(m => m.RecordedBy)
            .Where(m => m.AnimalId == animalId)
            .OrderByDescending(m => m.RecordDate).ThenByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

    public Task<MedicalRecord?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.MedicalRecords.AsNoTracking().Include(m => m.RecordedBy).FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task AddAsync(MedicalRecord record, CancellationToken ct = default) => await _db.MedicalRecords.AddAsync(record, ct);
}

public class DonationRepository : IDonationRepository
{
    private readonly AppDbContext _db;
    public DonationRepository(AppDbContext db) => _db = db;

    public Task<Donation?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Donations.Include(d => d.Donor).Include(d => d.Animal).FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<List<Donation>> GetForDonorAsync(Guid donorId, CancellationToken ct = default) =>
        _db.Donations.AsNoTracking().Include(d => d.Animal)
            .Where(d => d.DonorId == donorId)
            .OrderByDescending(d => d.DonatedAt)
            .ToListAsync(ct);

    public Task<List<Donation>> GetBetweenAsync(DateTime fromUtc, DateTime toUtc, DonationStatus? status = null, CancellationToken ct = default)
    {
        var q = _db.Donations.AsNoTracking().Include(d => d.Donor).Include(d => d.Animal).AsQueryable();
        if (status == DonationStatus.Received)
            // Received money is reported in the period it arrived, not when it was pledged.
            q = q.Where(d => d.Status == DonationStatus.Received && (d.ReceivedAt ?? d.DonatedAt) >= fromUtc && (d.ReceivedAt ?? d.DonatedAt) < toUtc);
        else
        {
            q = q.Where(d => d.DonatedAt >= fromUtc && d.DonatedAt < toUtc);
            if (status is { } s) q = q.Where(d => d.Status == s);
        }
        return q.OrderBy(d => d.DonatedAt).ToListAsync(ct);
    }

    public Task<List<Donation>> GetPledgedAsync(CancellationToken ct = default) =>
        _db.Donations.AsNoTracking().Include(d => d.Donor).Include(d => d.Animal)
            .Where(d => d.Status == DonationStatus.Pledged)
            .OrderBy(d => d.DonatedAt)
            .ToListAsync(ct);

    public Task<bool> HasReceivedAdoptionFeeAsync(Guid applicationId, CancellationToken ct = default) =>
        _db.Donations.AnyAsync(d => d.ApplicationId == applicationId && d.Type == DonationType.AdoptionFee
                                    && d.Status == DonationStatus.Received, ct);

    public Task<bool> ReceiptNumberExistsAsync(string receiptNumber, CancellationToken ct = default) =>
        _db.Donations.AnyAsync(d => d.ReceiptNumber == receiptNumber, ct);

    public async Task AddAsync(Donation donation, CancellationToken ct = default) => await _db.Donations.AddAsync(donation, ct);
}

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

public class BranchRepository : IBranchRepository
{
    private readonly AppDbContext _db;
    public BranchRepository(AppDbContext db) => _db = db;

    public Task<List<ShelterBranch>> GetAllAsync(CancellationToken ct = default) =>
        _db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync(ct);

    public Task<ShelterBranch?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Branches.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<ShelterBranch> GetDefaultAsync(CancellationToken ct = default) =>
        await _db.Branches.Where(b => b.IsActive).OrderBy(b => b.Name).FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException("No shelter branch exists. Run the database seeder.");

    public async Task AddAsync(ShelterBranch branch, CancellationToken ct = default) => await _db.Branches.AddAsync(branch, ct);
}

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    public UserRepository(AppDbContext db) => _db = db;

    public Task<ApplicationUser?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<List<ApplicationUser>> GetInRoleAsync(string role, CancellationToken ct = default) =>
        (from u in _db.Users.AsNoTracking()
         join ur in _db.UserRoles on u.Id equals ur.UserId
         join r in _db.Roles on ur.RoleId equals r.Id
         where r.Name == role
         orderby u.FirstName, u.LastName
         select u).ToListAsync(ct);

    public async Task<Dictionary<Guid, List<string>>> GetRolesByUserAsync(CancellationToken ct = default)
    {
        var rows = await (from ur in _db.UserRoles
                          join r in _db.Roles on ur.RoleId equals r.Id
                          select new { ur.UserId, r.Name }).ToListAsync(ct);
        return rows.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Name!).OrderBy(n => n).ToList());
    }

    public Task<List<ApplicationUser>> GetAllAsync(CancellationToken ct = default) =>
        _db.Users.AsNoTracking().Include(u => u.Branch).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync(ct);
}

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
