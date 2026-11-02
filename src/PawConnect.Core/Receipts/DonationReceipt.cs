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
