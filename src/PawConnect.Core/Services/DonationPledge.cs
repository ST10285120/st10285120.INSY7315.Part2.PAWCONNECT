using PawConnect.Core.Enums;

namespace PawConnect.Core.Services;

public record DonationPledge(decimal Amount, DonationFrequency Frequency, Guid? SponsorAnimalId, string? Note);
