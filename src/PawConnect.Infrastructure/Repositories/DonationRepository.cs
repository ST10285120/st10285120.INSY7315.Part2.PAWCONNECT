using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Infrastructure.Repositories;

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
