using System.ComponentModel.DataAnnotations;

namespace PawConnect.Web.ViewModels;

public class LoginWith2faVm
{
    [Required(ErrorMessage = "Enter the 6-digit code from your authenticator app, or a recovery code.")]
    [StringLength(20, MinimumLength = 6, ErrorMessage = "Enter the 6-digit code from your authenticator app, or a recovery code.")]
    [Display(Name = "Verification code")]
    public string Code { get; set; } = "";

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
