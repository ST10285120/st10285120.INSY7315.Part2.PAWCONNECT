using PawConnect.Core.Enums;

namespace PawConnect.Core.Entities;

public class Animal
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public ShelterBranch? Branch { get; set; }

    public string Name { get; set; } = "";
    public Species Species { get; set; }
    public string? Breed { get; set; }

    /// <summary>Estimated age in months at <see cref="IntakeDate"/>. Current age is derived.</summary>
    public int AgeMonthsAtIntake { get; set; }
    public AnimalSize Size { get; set; } = AnimalSize.Medium;
    public bool GoodWithKids { get; set; }
    public bool GoodWithOtherPets { get; set; }

    public AnimalStatus Status { get; set; } = AnimalStatus.Available;
    public bool IsVaccinated { get; set; }
    public int KennelNumber { get; set; }
    public string Bio { get; set; } = "";
    public DateOnly IntakeDate { get; set; }

    // Display colours/emoji kept from the prototype, used when no photo is uploaded yet.
    public string ColorFrom { get; set; } = "#7C9885";
    public string ColorTo { get; set; } = "#41614F";

    /// <summary>
    /// Optimistic concurrency token, changed on every update. If two people change the same
    /// record at the same moment (e.g. a volunteer approves while the adopter withdraws), the
    /// second save fails instead of silently overwriting the first.
    /// </summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<AnimalPhoto> Photos { get; set; } = new();
    public List<MedicalRecord> MedicalRecords { get; set; } = new();

    public string Emoji => Species switch { Species.Cat => "🐈", Species.Dog => "🐕", _ => "🐾" };

    /// <summary>Current age in whole months, based on the age at intake plus time in care.</summary>
    public int CurrentAgeMonths(DateOnly today)
    {
        var monthsInCare = (today.Year - IntakeDate.Year) * 12 + today.Month - IntakeDate.Month;
        return Math.Max(0, AgeMonthsAtIntake + Math.Max(0, monthsInCare));
    }

    public string AgeLabel(DateOnly today)
    {
        var months = CurrentAgeMonths(today);
        if (months < 12) return months <= 1 ? "1 mo" : $"{months} mo";
        var years = months / 12;
        return years == 1 ? "1 yr" : $"{years} yrs";
    }
}
