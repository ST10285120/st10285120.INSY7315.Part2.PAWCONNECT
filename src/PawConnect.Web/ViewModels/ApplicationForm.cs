using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

// ---------- Adoption application ----------

public class ApplicationForm
{
    public Guid AnimalId { get; set; }

    [Required, StringLength(120), Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required, StringLength(30), Phone, Display(Name = "Phone number")]
    public string Phone { get; set; } = "";

    [Required, StringLength(200), EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please tell us why you'd like to adopt."), StringLength(2000)]
    public string Why { get; set; } = "";

    [Required, StringLength(60), Display(Name = "Home type")]
    public string HomeType { get; set; } = "House with garden";

    [Required, StringLength(20), Display(Name = "Own or rent")]
    public string OwnRent { get; set; } = "Own";

    [Required, StringLength(20), Display(Name = "Other pets")]
    public string OtherPets { get; set; } = "None";

    [StringLength(2000)]
    public string? Notes { get; set; }

    public ApplicationSubmission ToSubmission() =>
        new(AnimalId, FullName, Phone, Email, Why, HomeType, OwnRent, OtherPets, Notes);

    public static readonly string[] HomeTypes = { "House with garden", "House with no garden", "Apartment or flat", "Farm or smallholding" };
    public static readonly string[] OwnRentOptions = { "Own", "Rent" };
    public static readonly string[] OtherPetsOptions = { "None", "1–2", "3+" };
}
