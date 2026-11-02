using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;
using PawConnect.Web.Security;

namespace PawConnect.Web.Controllers.Api;

[Route("api/auth")]
public class AuthApiController : ApiControllerBase
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;

    public AuthApiController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users)
    {
        _signIn = signIn;
        _users = users;
    }

    /// <summary>
    /// Exchanges email + password (+ an authenticator code when two-step verification is on) for a
    /// bearer token, for API clients and testing.
    /// </summary>
    [HttpPost("token"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Token(TokenRequest request)
    {
        // Check the password before revealing that an account is deactivated, so the API can't be
        // used to find out which email addresses have (deactivated) accounts.
        var user = await _users.FindByEmailAsync(request.Email);
        if (user is { IsActive: false } && !await _users.IsLockedOutAsync(user))
        {
            var passwordOk = await _users.CheckPasswordAsync(user, request.Password);
            if (!passwordOk) await _users.AccessFailedAsync(user); // wrong guesses still count toward lockout
            return Problem(title: passwordOk ? "Account deactivated. Contact the shelter." : "Incorrect email or password.", statusCode: 401);
        }

        _signIn.AuthenticationScheme = IdentityConstants.BearerScheme;
        var result = await _signIn.PasswordSignInAsync(request.Email, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.RequiresTwoFactor)
        {
            // Password was right and the account uses two-step verification: the code is needed too.
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
                return Problem(title: "Two-step verification code required.", statusCode: 401,
                    detail: "Send the 6-digit code from your authenticator app as twoFactorCode.");
            result = await _signIn.TwoFactorAuthenticatorSignInAsync(
                request.TwoFactorCode.Replace(" ", ""), isPersistent: false, rememberClient: false);
        }
        if (!result.Succeeded)
            return Problem(title: result.IsLockedOut ? "Account locked. Try again in 15 minutes." : "Incorrect email or password.", statusCode: 401);
        return new EmptyResult(); // the bearer handler has already written { accessToken, expiresIn, refreshToken }
    }
}
