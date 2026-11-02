using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class DonateForm
{
    [Range(typeof(decimal), "10", "100000", ErrorMessage = "Please enter an amount between R10 and R100,000.")]
    public decimal Amount { get; set; } = 250;

    public DonationFrequency Frequency { get; set; } = DonationFrequency.OnceOff;

    public Guid? SponsorAnimalId { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
