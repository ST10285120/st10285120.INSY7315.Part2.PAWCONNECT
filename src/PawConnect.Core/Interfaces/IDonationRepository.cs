using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

public interface IDonationRepository
{
    Task<Donation?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<Donation>> GetForDonorAsync(Guid donorId, CancellationToken ct = default);
    Task<List<Donation>> GetBetweenAsync(DateTime fromUtc, DateTime toUtc, DonationStatus? status = null, CancellationToken ct = default);
    Task<List<Donation>> GetPledgedAsync(CancellationToken ct = default);
    Task<bool> HasReceivedAdoptionFeeAsync(Guid applicationId, CancellationToken ct = default);
    Task<bool> ReceiptNumberExistsAsync(string receiptNumber, CancellationToken ct = default);
    Task AddAsync(Donation donation, CancellationToken ct = default);
}
