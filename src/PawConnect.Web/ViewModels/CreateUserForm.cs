using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class CreateUserForm
{
    [Required, StringLength(60), Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required, StringLength(60), Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = "";

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required]
    public string Role { get; set; } = Core.Common.Roles.Volunteer;

    [StringLength(60), Display(Name = "Volunteer activity")]
    public string? VolunteerRole { get; set; }

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "Temporary password")]
    public string TemporaryPassword { get; set; } = "";
}
