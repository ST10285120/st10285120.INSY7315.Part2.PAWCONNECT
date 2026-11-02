using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PawConnect.Web.ViewModels;

public class TwoFactorVm
{
    public bool IsEnabled { get; set; }

    /// <summary>True when this account must use two-step verification (administrators in production).</summary>
    public bool IsRequired { get; set; }

    /// <summary>Display only (never posted back). The authenticator key, grouped in fours so it is easy to type into an app.</summary>
    [ValidateNever] public string SharedKey { get; set; } = "";

    /// <summary>otpauth:// link: opens the authenticator app directly on a phone.</summary>
    [ValidateNever] public string AuthenticatorUri { get; set; } = "";

    public int RecoveryCodesLeft { get; set; }

    /// <summary>Shown once, straight after they are generated.</summary>
    public string[]? NewRecoveryCodes { get; set; }

    [Required(ErrorMessage = "Enter the 6-digit code your authenticator app shows.")]
    [RegularExpression(@"^\s*\d{3}\s?\d{3}\s*$", ErrorMessage = "The code is 6 digits.")]
    [Display(Name = "6-digit code")]
    public string Code { get; set; } = "";
}
