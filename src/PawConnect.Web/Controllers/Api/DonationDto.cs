using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

public record DonationDto(Guid Id, string ReceiptNumber, decimal Amount, string Type, string Frequency, string Status, DateTime DonatedAt, string? AnimalName)
{
    public static DonationDto From(Donation d) => new(
        d.Id, d.ReceiptNumber, d.Amount, d.Type.ToString(), d.Frequency.ToString(), d.Status.ToString(), d.DonatedAt, d.Animal?.Name);
}
