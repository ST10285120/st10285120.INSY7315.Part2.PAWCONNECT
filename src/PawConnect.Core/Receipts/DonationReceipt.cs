using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Receipts;

public record DonationReceipt(
    string ReceiptNumber,
    DateTime IssuedAtUtc,
    string DonorName,
    decimal Amount,
    string Description,
    string StatusLine,
    string ShelterName)
{
    public string AmountDisplay => Amount.ToString("C", ZarCulture.Instance);

    public string ToPlainText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{ShelterName} - Donation acknowledgement");
        sb.AppendLine($"Receipt no: {ReceiptNumber}");
        sb.AppendLine($"Date: {ShelterTime.ToLocal(IssuedAtUtc):d MMMM yyyy}");
        sb.AppendLine($"Donor: {DonorName}");
        sb.AppendLine($"Amount: {AmountDisplay}");
        sb.AppendLine($"For: {Description}");
        sb.AppendLine(StatusLine);
        sb.AppendLine();
        sb.AppendLine("Thank you for supporting the animals in our care.");
        return sb.ToString();
    }
}

public interface IDonationReceiptFactory
{
    string NewReceiptNumber(DateTime utcNow);
    DonationReceipt Create(Donation donation, string donorName, string? animalName);
}

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

/// <summary>South African Rand formatting ("R 1 250,00" style is avoided for readability).</summary>
public static class ZarCulture
{
    public static readonly CultureInfo Instance = Create();

    private static CultureInfo Create()
    {
        var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        c.NumberFormat.CurrencySymbol = "R";
        c.NumberFormat.CurrencyPositivePattern = 0; // R1,250.00
        c.NumberFormat.CurrencyNegativePattern = 1;
        c.NumberFormat.CurrencyDecimalDigits = 2;
        return c;
    }
}
