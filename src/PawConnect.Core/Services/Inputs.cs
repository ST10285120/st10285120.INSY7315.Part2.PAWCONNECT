using PawConnect.Core.Enums;

namespace PawConnect.Core.Services;

// Plain input records passed from the web layer (MVC forms or the REST API) to the services.
// The services validate them again, so the rules hold however the data arrives.

public record ApplicationSubmission(
    Guid AnimalId,
    string FullName,
    string Phone,
    string Email,
    string Why,
    string HomeType,
    string OwnRent,
    string OtherPets,
    string? Notes);

public record AnimalInput(
    string Name,
    Species Species,
    string? Breed,
    int AgeMonths,
    AnimalSize Size,
    bool GoodWithKids,
    bool GoodWithOtherPets,
    bool IsVaccinated,
    int KennelNumber,
    string Bio,
    DateOnly? IntakeDate,
    Guid? BranchId);

public record MedicalRecordInput(
    MedicalRecordType RecordType,
    string Title,
    string? Details,
    string? VetName,
    DateOnly RecordDate,
    DateOnly? NextDueDate);

public record ShiftInput(
    DateTime StartsAtLocal,
    DateTime EndsAtLocal,
    string Role,
    int Capacity,
    string? Notes,
    Guid? BranchId);

public record HourLogInput(DateOnly Date, decimal Hours, string Activity, string? Notes);

public record DonationPledge(decimal Amount, DonationFrequency Frequency, Guid? SponsorAnimalId, string? Note);
