using PawConnect.Core.Enums;

namespace PawConnect.Core.Entities;

/// <summary>
/// General donations, animal sponsorships and adoption fees all live in one table, with
/// optional links to an animal and/or an application (see ERD, section 5.2).
/// </summary>
public class Donation
{
    public Guid Id { get; set; }

    public Guid DonorId { get; set; }
    public ApplicationUser? Donor { get; set; }

    public Guid? AnimalId { get; set; }
    public Animal? Animal { get; set; }

    public Guid? ApplicationId { get; set; }
    public AdoptionApplication? Application { get; set; }

    public decimal Amount { get; set; }
    public DonationType Type { get; set; }
    public DonationFrequency Frequency { get; set; }
    public DonationStatus Status { get; set; } = DonationStatus.Pledged;

    public DateTime DonatedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public Guid? RecordedById { get; set; }

    public string ReceiptNumber { get; set; } = "";
    public string? Note { get; set; }
}
