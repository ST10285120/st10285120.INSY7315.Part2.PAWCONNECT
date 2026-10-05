using PawConnect.Core.Enums;

namespace PawConnect.Core.Entities;

public class AdoptionApplication
{
    public Guid Id { get; set; }

    /// <summary>Human friendly reference shown to applicants, e.g. PC-260926-7KQ4.</summary>
    public string ReferenceNumber { get; set; } = "";

    public Guid AnimalId { get; set; }
    public Animal? Animal { get; set; }

    public Guid AdopterId { get; set; }
    public ApplicationUser? Adopter { get; set; }

    public Guid? ReviewedById { get; set; }
    public ApplicationUser? ReviewedBy { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public DateTime SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Details captured by the multi-step form.
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Why { get; set; } = "";
    public string HomeType { get; set; } = "";
    public string OwnRent { get; set; } = "";
    public string OtherPets { get; set; } = "";
    public string? Notes { get; set; }

    // Filled in by staff during review.
    public string? ReviewNotes { get; set; }
    public DateTime? HomeVisitCompletedAt { get; set; }
    public string? HomeVisitNotes { get; set; }

    /// <summary>
    /// Optimistic concurrency token, changed on every update. If two people change the same
    /// record at the same moment (e.g. a volunteer approves while the adopter withdraws), the
    /// second save fails instead of silently overwriting the first.
    /// </summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public List<ApplicationStatusChange> StatusHistory { get; set; } = new();
    public List<Donation> Donations { get; set; } = new();

    public bool IsActive => Status is ApplicationStatus.Submitted or ApplicationStatus.UnderReview or ApplicationStatus.Approved;
}
