using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Receipts;

/// <summary>
/// Factory pattern (design doc, section 6.1): builds the receipt / acknowledgement for a
/// donation. All receipt formatting lives here instead of being repeated in the donation
/// page, the email and the admin screens.
/// </summary>
public class DonationReceiptFactory : IDonationReceiptFactory
{
    public const string ShelterName = "Hope & Paws Animal Shelter";
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I to avoid misreading

    public string NewReceiptNumber(DateTime utcNow)
    {
        var suffix = new char[6];
        for (var i = 0; i < suffix.Length; i++)
            suffix[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return $"RCPT-{ShelterTime.ToLocal(utcNow):yyyyMMdd}-{new string(suffix)}";
    }

    public DonationReceipt Create(Donation donation, string donorName, string? animalName)
    {
        var description = donation.Type switch
        {
            DonationType.Sponsorship => $"Sponsorship of {animalName ?? "a shelter animal"}'s care",
            DonationType.AdoptionFee => $"Adoption fee{(animalName is null ? "" : $" for {animalName}")}",
            _ => "General donation - used where it's needed most"
        };
        if (donation.Frequency == DonationFrequency.Monthly)
            description += " (monthly)";

        var status = donation.Status switch
        {
            DonationStatus.Received => $"Status: Received on {ShelterTime.ToLocal(donation.ReceivedAt ?? donation.DonatedAt):d MMMM yyyy}.",
            DonationStatus.Cancelled => "Status: Cancelled.",
            _ => "Status: Pledged. Please pay by EFT using the receipt number as reference; we'll confirm once it clears."
        };

        return new DonationReceipt(
            donation.ReceiptNumber,
            donation.ReceivedAt ?? donation.DonatedAt,
            string.IsNullOrWhiteSpace(donorName) ? "Supporter" : donorName,
            donation.Amount,
            description,
            status,
            ShelterName);
    }
}
