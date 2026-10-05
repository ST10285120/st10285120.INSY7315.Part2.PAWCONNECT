using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

// Data transfer objects: the JSON shapes the API sends and receives. Entities are never
// serialised directly, so internal fields (e.g. password hashes, photo bytes) can't leak.

public record AnimalDto(
    Guid Id, string Name, string Species, string? Breed, string Age, int AgeMonths, string Size, string Status,
    bool IsVaccinated, bool GoodWithKids, bool GoodWithOtherPets, int KennelNumber, string Bio,
    string? PhotoUrl, string ColorFrom, string ColorTo, string Emoji, string ProfileUrl)
{
    public static AnimalDto From(Animal a, Guid? photoId, DateOnly today) => new(
        a.Id, a.Name, a.Species.ToString(), a.Breed, a.AgeLabel(today), a.CurrentAgeMonths(today), a.Size.ToString(),
        a.Status.ToString(), a.IsVaccinated, a.GoodWithKids, a.GoodWithOtherPets, a.KennelNumber, a.Bio,
        photoId is { } p ? $"/media/photos/{p}" : null, a.ColorFrom, a.ColorTo, a.Emoji, $"/Home/Profile/{a.Id}");
}

public record MedicalRecordDto(
    Guid Id, string RecordType, string Title, string? Details, string? VetName,
    DateOnly RecordDate, DateOnly? NextDueDate, string RecordedBy, DateTime CreatedAt)
{
    public static MedicalRecordDto From(MedicalRecord m) => new(
        m.Id, m.RecordType.ToString(), m.Title, m.Details, m.VetName, m.RecordDate, m.NextDueDate,
        m.RecordedBy?.FullName ?? "", m.CreatedAt);
}

public record AnimalDetailDto(AnimalDto Animal, List<MedicalRecordDto>? MedicalHistory);

public class CreateMedicalRecordRequest
{
    [Required] public Guid AnimalId { get; set; }
    [Required] public MedicalRecordType RecordType { get; set; }
    [Required, StringLength(120)] public string Title { get; set; } = "";
    [StringLength(2000)] public string? Details { get; set; }
    [StringLength(120)] public string? VetName { get; set; }
    [Required] public DateOnly RecordDate { get; set; }
    public DateOnly? NextDueDate { get; set; }

    public MedicalRecordInput ToInput() => new(RecordType, Title, Details, VetName, RecordDate, NextDueDate);
}

public record ApplicationDto(
    Guid Id, string ReferenceNumber, Guid AnimalId, string? AnimalName, string Status, DateTime SubmittedAt,
    DateTime? DecidedAt, string? ReviewNotes, bool HomeVisitCompleted)
{
    public static ApplicationDto From(AdoptionApplication a) => new(
        a.Id, a.ReferenceNumber, a.AnimalId, a.Animal?.Name, a.Status.ToString(), a.SubmittedAt, a.DecidedAt,
        a.ReviewNotes, a.HomeVisitCompletedAt != null);
}

public class SubmitApplicationRequest
{
    [Required] public Guid AnimalId { get; set; }
    [Required, StringLength(120)] public string FullName { get; set; } = "";
    [Required, StringLength(30)] public string Phone { get; set; } = "";
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = "";
    [Required, StringLength(2000)] public string Why { get; set; } = "";
    [Required, StringLength(60)] public string HomeType { get; set; } = "";
    [Required, StringLength(20)] public string OwnRent { get; set; } = "";
    [Required, StringLength(20)] public string OtherPets { get; set; } = "";
    [StringLength(2000)] public string? Notes { get; set; }

    public ApplicationSubmission ToSubmission() => new(AnimalId, FullName, Phone, Email, Why, HomeType, OwnRent, OtherPets, Notes);
}

public class TransitionRequest
{
    /// <summary>Target status: UnderReview, Approved, Rejected or Adopted.</summary>
    [Required] public ApplicationStatus Status { get; set; }
    [StringLength(2000)] public string? Note { get; set; }
}

public record ShiftDto(Guid Id, DateTime StartsAt, DateTime EndsAt, string Role, int Capacity, int Filled, bool IsFull, bool SignedUp)
{
    public static ShiftDto From(Shift s, Guid userId) =>
        new(s.Id, s.StartsAt, s.EndsAt, s.Role, s.Capacity, s.Filled, s.IsFull, s.Signups.Any(x => x.VolunteerId == userId));
}

public class DonationRequest
{
    [Range(typeof(decimal), "10", "100000")] public decimal Amount { get; set; }
    public DonationFrequency Frequency { get; set; } = DonationFrequency.OnceOff;
    public Guid? SponsorAnimalId { get; set; }
    [StringLength(500)] public string? Note { get; set; }
}

public record DonationDto(Guid Id, string ReceiptNumber, decimal Amount, string Type, string Frequency, string Status, DateTime DonatedAt, string? AnimalName)
{
    public static DonationDto From(Donation d) => new(
        d.Id, d.ReceiptNumber, d.Amount, d.Type.ToString(), d.Frequency.ToString(), d.Status.ToString(), d.DonatedAt, d.Animal?.Name);
}

public class TokenRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}
