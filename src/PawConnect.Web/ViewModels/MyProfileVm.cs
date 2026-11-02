using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

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
