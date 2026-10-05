using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

// ---------- Public pages ----------

public record AnimalCardVm(Animal Animal, Guid? PhotoId, DateOnly Today);

public class HomeVm
{
    public AnimalCardVm? Featured { get; init; }
    public List<AnimalCardVm> ReadyToMeet { get; init; } = new();
    public int AdoptedThisYear { get; init; }
    public int AnimalsInCare { get; init; }
    public int ActiveVolunteers { get; init; }
    public decimal DonatedThisMonth { get; init; }
}

public class BrowseVm
{
    public List<AnimalCardVm> Animals { get; init; } = new();
    public string Species { get; init; } = "all";
    public string Size { get; init; } = "all";
    public bool AvailableOnly { get; init; }
    public bool GoodWithKids { get; init; }
    public bool GoodWithPets { get; init; }
    public string Search { get; init; } = "";
    public int InCareCount { get; init; }
}

public class ProfileVm
{
    public required Animal Animal { get; init; }
    public List<AnimalPhotoInfo> Photos { get; init; } = new();
    public DateOnly Today { get; init; }
    public bool IsStaff { get; init; }
    public bool IsAdmin { get; init; }
}

// ---------- Adoption application ----------

public class ApplicationForm
{
    public Guid AnimalId { get; set; }

    [Required, StringLength(120), Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required, StringLength(30), Phone, Display(Name = "Phone number")]
    public string Phone { get; set; } = "";

    [Required, StringLength(200), EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please tell us why you'd like to adopt."), StringLength(2000)]
    public string Why { get; set; } = "";

    [Required, StringLength(60), Display(Name = "Home type")]
    public string HomeType { get; set; } = "House with garden";

    [Required, StringLength(20), Display(Name = "Own or rent")]
    public string OwnRent { get; set; } = "Own";

    [Required, StringLength(20), Display(Name = "Other pets")]
    public string OtherPets { get; set; } = "None";

    [StringLength(2000)]
    public string? Notes { get; set; }

    public ApplicationSubmission ToSubmission() =>
        new(AnimalId, FullName, Phone, Email, Why, HomeType, OwnRent, OtherPets, Notes);

    public static readonly string[] HomeTypes = { "House with garden", "House with no garden", "Apartment or flat", "Farm or smallholding" };
    public static readonly string[] OwnRentOptions = { "Own", "Rent" };
    public static readonly string[] OtherPetsOptions = { "None", "1–2", "3+" };
}

public class ApplyVm
{
    public required Animal Animal { get; init; }
    public required ApplicationForm Form { get; init; }
}

// ---------- Donations ----------

public class DonateForm
{
    [Range(typeof(decimal), "10", "100000", ErrorMessage = "Please enter an amount between R10 and R100,000.")]
    public decimal Amount { get; set; } = 250;

    public DonationFrequency Frequency { get; set; } = DonationFrequency.OnceOff;

    public Guid? SponsorAnimalId { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

public class DonateVm
{
    public DonateForm Form { get; init; } = new();
    public List<Animal> SponsorableAnimals { get; init; } = new();
}

// ---------- Account ----------

public class LoginVm
{
    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterVm
{
    [Required, StringLength(60), Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required, StringLength(60), Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required, EmailAddress, StringLength(200), Display(Name = "Email address")]
    public string Email { get; set; } = "";

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "The passwords don't match."), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    /// <summary>POPIA consent (design doc, section 7.4).</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Please accept the privacy notice to create an account.")]
    public bool AcceptPrivacy { get; set; }

    public string? ReturnUrl { get; set; }
}

public class MyProfileVm
{
    [Required, StringLength(60), Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required, StringLength(60), Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    // Display-only values: not posted back, so they are excluded from validation.
    [ValidateNever] public string Email { get; set; } = "";
    [ValidateNever] public List<string> Roles { get; set; } = new();
    [ValidateNever] public List<Donation> Donations { get; set; } = new();
}

// ---------- Volunteer ----------

public class HourLogForm
{
    [Required]
    public DateOnly Date { get; set; }

    [Range(0.25, 24, ErrorMessage = "Hours must be between 0.25 and 24.")]
    public decimal Hours { get; set; }

    [Required, StringLength(60)]
    public string Activity { get; set; } = "Dog walking";

    [StringLength(500)]
    public string? Notes { get; set; }

    public static readonly string[] Activities = { "Dog walking", "Cattery cleaning", "Front desk", "Events & fundraising", "Transport", "General" };
}

public record ShiftRowVm(Shift Shift, bool IsMine);

public class VolunteerDashboardVm
{
    public string FirstName { get; init; } = "";
    public List<AdoptionApplication> OpenApplications { get; init; } = new();
    public List<ShiftRowVm> Shifts { get; init; } = new();
    public List<HourLog> MyHours { get; init; } = new();
    public decimal MyApprovedTotal { get; init; }
    public HourLogForm HourForm { get; init; } = new();
    public bool IsAdmin { get; init; }
}

public class ApplicationDetailVm
{
    public required AdoptionApplication Application { get; init; }
    public bool FeeRecorded { get; init; }
    public bool IsAdmin { get; init; }
    public bool CanStartReview => Application.Status == ApplicationStatus.Submitted;
    public bool CanDecide => Application.Status == ApplicationStatus.UnderReview;
    public bool CanReject => ApplicationStateMachine.CanTransition(Application.Status, ApplicationStatus.Rejected);
    public bool IsApproved => Application.Status == ApplicationStatus.Approved;
    public bool CanComplete => IsApproved && Application.HomeVisitCompletedAt != null && FeeRecorded;
}

// ---------- Administrator ----------

public class AdminDashboardVm
{
    public required DashboardStats Stats { get; init; }
    public List<AdoptionApplication> RecentApplications { get; init; } = new();
    public List<MonthlyTotal> MonthlyDonations { get; init; } = new();
}

public class AnimalForm
{
    public Guid? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    public Species Species { get; set; } = Species.Dog;

    [StringLength(100)]
    public string? Breed { get; set; }

    [Range(0, 360, ErrorMessage = "Age must be between 0 and 360 months."), Display(Name = "Age (months)")]
    public int AgeMonths { get; set; } = 12;

    public AnimalSize Size { get; set; } = AnimalSize.Medium;

    [Display(Name = "Good with kids")]
    public bool GoodWithKids { get; set; }

    [Display(Name = "Good with other pets")]
    public bool GoodWithOtherPets { get; set; }

    [Display(Name = "Vaccinated")]
    public bool IsVaccinated { get; set; }

    [Range(1, 9999), Display(Name = "Kennel number")]
    public int KennelNumber { get; set; }

    [StringLength(2000)]
    public string? Bio { get; set; }

    [Display(Name = "Intake date")]
    public DateOnly? IntakeDate { get; set; }

    public Guid? BranchId { get; set; }

    public AnimalInput ToInput() => new(Name, Species, Breed, AgeMonths, Size, GoodWithKids, GoodWithOtherPets,
        IsVaccinated, KennelNumber, Bio ?? "", IntakeDate, BranchId);

    public static AnimalForm From(Animal a) => new()
    {
        Id = a.Id, Name = a.Name, Species = a.Species, Breed = a.Breed, AgeMonths = a.AgeMonthsAtIntake, Size = a.Size,
        GoodWithKids = a.GoodWithKids, GoodWithOtherPets = a.GoodWithOtherPets, IsVaccinated = a.IsVaccinated,
        KennelNumber = a.KennelNumber, Bio = a.Bio, IntakeDate = a.IntakeDate, BranchId = a.BranchId
    };
}

public class AdminAnimalsVm
{
    public List<Animal> Animals { get; init; } = new();
    public AnimalForm NewAnimal { get; init; } = new();
    public List<ShelterBranch> Branches { get; init; } = new();
}

public class EditAnimalVm
{
    public required AnimalForm Form { get; init; }
    public List<AnimalPhotoInfo> Photos { get; init; } = new();
    public List<ShelterBranch> Branches { get; init; } = new();
    public AnimalStatus Status { get; init; }
}

public record VolunteerRowVm(ApplicationUser Volunteer, decimal ApprovedHours, int PendingLogs);

public class HoursAdminVm
{
    public List<HourLog> Logs { get; init; } = new();
    public List<ApplicationUser> Volunteers { get; init; } = new();
    public Guid? SelectedVolunteerId { get; init; }
    public string SelectedStatus { get; init; } = "all";
    public decimal ApprovedThisMonth { get; init; }
    public int PendingCount { get; init; }
    public List<(string Name, decimal Hours)> ChartData { get; init; } = new();
}

public record UserRowVm(ApplicationUser User, List<string> Roles);

public class CreateUserForm
{
    [Required, StringLength(60), Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required, StringLength(60), Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = "";

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required]
    public string Role { get; set; } = Core.Common.Roles.Volunteer;

    [StringLength(60), Display(Name = "Volunteer activity")]
    public string? VolunteerRole { get; set; }

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "Temporary password")]
    public string TemporaryPassword { get; set; } = "";
}

public class UsersAdminVm
{
    public List<UserRowVm> Users { get; init; } = new();
    public CreateUserForm NewUser { get; init; } = new();
    public string Filter { get; init; } = "all";
    public Guid CurrentUserId { get; init; }
    public List<ShelterBranch> Branches { get; init; } = new();
}

public class ShiftForm
{
    [Required, Display(Name = "Date")]
    public DateOnly Date { get; set; }

    [Required, Display(Name = "Start time")]
    public TimeOnly StartTime { get; set; } = new(9, 0);

    [Required, Display(Name = "End time")]
    public TimeOnly EndTime { get; set; } = new(12, 0);

    [Required, StringLength(60)]
    public string Role { get; set; } = "Dog walking";

    [Range(1, 50)]
    public int Capacity { get; set; } = 3;

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class ShiftsAdminVm
{
    public List<Shift> Shifts { get; init; } = new();
    public ShiftForm NewShift { get; init; } = new();
}

public class DonationsAdminVm
{
    public List<Donation> Pledged { get; init; } = new();
    public List<Donation> RecentlyReceived { get; init; } = new();
}

public class ReportVm
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public ReportSummary? Summary { get; init; }
    public string? Error { get; init; }
}
