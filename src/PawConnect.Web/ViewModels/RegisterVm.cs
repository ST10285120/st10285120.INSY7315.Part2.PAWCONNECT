using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class RegisterVm
{
    [Required, StringLength(60), Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required, StringLength(60), Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required, EmailAddress, StringLength(200), Display(Name = "Email address")]
    public string Email { get; set; } = "";

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "The passwords don't match."), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    /// <summary>POPIA consent (design doc, section 7.4).</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Please accept the privacy notice to create an account.")]
    public bool AcceptPrivacy { get; set; }

    public string? ReturnUrl { get; set; }
}
