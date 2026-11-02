using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

public class DonationRequest
{
    [Range(typeof(decimal), "10", "100000")] public decimal Amount { get; set; }
    public DonationFrequency Frequency { get; set; } = DonationFrequency.OnceOff;
    public Guid? SponsorAnimalId { get; set; }
    [StringLength(500)] public string? Note { get; set; }
}
