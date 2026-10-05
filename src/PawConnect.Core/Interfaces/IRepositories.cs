using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

// Repository pattern (design doc, section 6.1): every entity has a repository that hides
// the database queries. Services and controllers only see these interfaces, so business
// logic can be unit tested without a real PostgreSQL server.

public record AnimalSearch(
    Species? Species = null,
    AnimalSize? Size = null,
    bool AvailableOnly = false,
    bool? GoodWithKids = null,
    bool? GoodWithOtherPets = null,
    string? Text = null,
    Guid? BranchId = null,
    bool IncludeAdopted = true);

public record AnimalPhotoInfo(Guid Id, Guid AnimalId, bool IsPrimary, DateTime UploadedAt);

public interface IAnimalRepository
{
    Task<List<Animal>> SearchAsync(AnimalSearch search, CancellationToken ct = default);
    Task<Animal?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Animal?> GetWithMedicalHistoryAsync(Guid id, CancellationToken ct = default);
    Task<int> CountInCareAsync(CancellationToken ct = default);
    Task<bool> KennelInUseAsync(Guid branchId, int kennelNumber, Guid? exceptAnimalId, CancellationToken ct = default);
    Task AddAsync(Animal animal, CancellationToken ct = default);

    Task<Dictionary<Guid, Guid>> GetPrimaryPhotoIdsAsync(IEnumerable<Guid> animalIds, CancellationToken ct = default);
    Task<List<AnimalPhotoInfo>> GetPhotoInfosAsync(Guid animalId, CancellationToken ct = default);
    Task<AnimalPhoto?> GetPhotoAsync(Guid photoId, CancellationToken ct = default);
    Task AddPhotoAsync(AnimalPhoto photo, CancellationToken ct = default);
    void RemovePhoto(AnimalPhoto photo);
}

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

public interface IMedicalRecordRepository
{
    Task<List<MedicalRecord>> GetForAnimalAsync(Guid animalId, CancellationToken ct = default);
    Task<MedicalRecord?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(MedicalRecord record, CancellationToken ct = default);
}

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

public record HourLogFilter(Guid? VolunteerId = null, HourLogStatus? Status = null);

public interface IHourLogRepository
{
    Task<HourLog?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<HourLog>> FindAsync(HourLogFilter filter, CancellationToken ct = default);
    Task<decimal> SumApprovedAsync(Guid? volunteerId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<Dictionary<Guid, decimal>> ApprovedTotalsByVolunteerAsync(CancellationToken ct = default);
    Task<int> CountPendingAsync(Guid? volunteerId = null, CancellationToken ct = default);
    Task AddAsync(HourLog log, CancellationToken ct = default);
}

public interface IBranchRepository
{
    Task<List<ShelterBranch>> GetAllAsync(CancellationToken ct = default);
    Task<ShelterBranch?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ShelterBranch> GetDefaultAsync(CancellationToken ct = default);
    Task AddAsync(ShelterBranch branch, CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<ApplicationUser?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<ApplicationUser>> GetInRoleAsync(string role, CancellationToken ct = default);
    Task<Dictionary<Guid, List<string>>> GetRolesByUserAsync(CancellationToken ct = default);
    Task<List<ApplicationUser>> GetAllAsync(CancellationToken ct = default);
}

public interface INotificationRepository
{
    Task AddAsync(NotificationMessage message, CancellationToken ct = default);
    Task<List<NotificationMessage>> GetRecentAsync(int count, CancellationToken ct = default);
}

/// <summary>Commits all repository changes made during a request as one unit.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Forgets tracked (possibly stale) entities, e.g. before retrying after a concurrency conflict.</summary>
    void DiscardChanges();
}

/// <summary>Raised by the unit of work when an optimistic concurrency check fails.</summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? inner = null) : base(message, inner) { }
}
