using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Infrastructure.Repositories;

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
