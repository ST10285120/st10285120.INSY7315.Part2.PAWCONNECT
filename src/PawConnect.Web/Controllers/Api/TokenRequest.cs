using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

public class TokenRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";

    /// <summary>6-digit authenticator code. Required only for accounts with two-step verification on.</summary>
    public string? TwoFactorCode { get; set; }
}
