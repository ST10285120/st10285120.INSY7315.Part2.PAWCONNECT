using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

public class SubmitApplicationRequest
{
    [Required] public Guid AnimalId { get; set; }
    [Required, StringLength(120)] public string FullName { get; set; } = "";
    [Required, StringLength(30)] public string Phone { get; set; } = "";
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = "";
    [Required, StringLength(2000)] public string Why { get; set; } = "";
    [Required, StringLength(60)] public string HomeType { get; set; } = "";
    [Required, StringLength(20)] public string OwnRent { get; set; } = "";
    [Required, StringLength(20)] public string OtherPets { get; set; } = "";
    [StringLength(2000)] public string? Notes { get; set; }

    public ApplicationSubmission ToSubmission() => new(AnimalId, FullName, Phone, Email, Why, HomeType, OwnRent, OtherPets, Notes);
}
