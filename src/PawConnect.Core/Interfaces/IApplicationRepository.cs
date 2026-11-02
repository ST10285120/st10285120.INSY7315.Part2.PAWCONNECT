using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

public interface IApplicationRepository
{
    Task<AdoptionApplication?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<AdoptionApplication>> GetForAdopterAsync(Guid adopterId, CancellationToken ct = default);
    Task<List<AdoptionApplication>> GetOpenForReviewAsync(Guid? branchId, CancellationToken ct = default);
    Task<List<AdoptionApplication>> GetRecentAsync(int count, CancellationToken ct = default);
    Task<bool> HasActiveApplicationForAnimalAsync(Guid animalId, CancellationToken ct = default);
    Task<bool> ReferenceExistsAsync(string reference, CancellationToken ct = default);
    Task<int> CountAdoptedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<int> CountSubmittedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<double?> AverageDecisionHoursAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task AddAsync(AdoptionApplication application, CancellationToken ct = default);
}
