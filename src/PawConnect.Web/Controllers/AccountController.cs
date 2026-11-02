using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Interfaces;
using PawConnect.Web.Security;
using PawConnect.Web.ViewModels;

namespace PawConnect.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IDonationRepository _donations;
    private readonly IApplicationRepository _applications;
    private readonly IHourLogRepository _hours;
    private readonly IClock _clock;
    private readonly ILogger<AccountController> _logger;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        IDonationRepository donations, IApplicationRepository applications, IHourLogRepository hours,
        IClock clock, ILogger<AccountController> logger, IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
        _users = users;
        _signIn = signIn;
        _donations = donations;
        _applications = applications;
        _hours = hours;
        _clock = clock;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl) => View(new LoginVm { ReturnUrl = returnUrl });

    [HttpPost, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _users.FindByEmailAsync(vm.Email);
        if (user is { IsActive: false } && !await _users.IsLockedOutAsync(user))
        {
            // Only say "deactivated" to someone who knows the password, so the login form can't be
            // used to discover which email addresses have accounts.
            var passwordOk = await _users.CheckPasswordAsync(user, vm.Password);
            if (!passwordOk) await _users.AccessFailedAsync(user); // wrong guesses still count toward lockout
            ModelState.AddModelError("", passwordOk
                ? "This account has been deactivated. Please contact the shelter."
                : "Incorrect email or password.");
            return View(vm);
        }

        var result = await _signIn.PasswordSignInAsync(vm.Email, vm.Password, vm.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            _logger.LogInformation("User {UserId} signed in", user?.Id);
            TempData["Toast"] = $"Welcome back{(user != null ? ", " + user.FirstName : "")}!";
            return LocalRedirect(SafeReturnUrl(vm.ReturnUrl) ?? await HomeFor(user!));
        }
        if (result.RequiresTwoFactor)
        {
            // Password was right; Identity has remembered who is signing in. Now ask for the code.
            return RedirectToAction(nameof(LoginWith2fa), new { returnUrl = SafeReturnUrl(vm.ReturnUrl), rememberMe = vm.RememberMe });
        }
        if (result.IsLockedOut)
        {
            _logger.LogWarning("Account locked out for {Email}", vm.Email);
            ModelState.AddModelError("", "Too many failed attempts. Your account is locked for 15 minutes.");
        }
        else
        {
            // Same message whether the email exists or not, so accounts can't be enumerated.
            ModelState.AddModelError("", "Incorrect email or password.");
        }
        return View(vm);
    }

    // ---------- Two-step verification (TOTP authenticator app) ----------

    [HttpGet]
    public async Task<IActionResult> LoginWith2fa(string? returnUrl, bool rememberMe)
    {
        // Only reachable straight after a correct password for an account with two-step verification.
        if (await _signIn.GetTwoFactorAuthenticationUserAsync() == null) return RedirectToAction(nameof(Login));
        return View(new LoginWith2faVm { ReturnUrl = returnUrl, RememberMe = rememberMe });
    }

    [HttpPost, EnableRateLimiting("auth")]
    public async Task<IActionResult> LoginWith2fa(LoginWith2faVm vm)
    {
        var user = await _signIn.GetTwoFactorAuthenticationUserAsync();
        if (user == null) return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid) return View(vm);

        var code = vm.Code.Replace(" ", "").Replace("-", "").Trim();
        // 6 digits = authenticator app; anything else is treated as one of the single-use recovery codes.
        var result = code.Length == 6 && code.All(char.IsDigit)
            ? await _signIn.TwoFactorAuthenticatorSignInAsync(code, vm.RememberMe, rememberClient: false)
            : await _signIn.TwoFactorRecoveryCodeSignInAsync(vm.Code.Replace(" ", "").Trim());

        if (result.Succeeded)
        {
            _logger.LogInformation("User {UserId} signed in with two-step verification", user.Id);
            TempData["Toast"] = $"Welcome back, {user.FirstName}!";
            return LocalRedirect(SafeReturnUrl(vm.ReturnUrl) ?? await HomeFor(user));
        }
        if (result.IsLockedOut)
        {
            _logger.LogWarning("Account {UserId} locked out at the verification step", user.Id);
            ModelState.AddModelError("", "Too many failed attempts. Your account is locked for 15 minutes.");
        }
        else
        {
            ModelState.AddModelError(nameof(vm.Code), "That code didn't work. Codes change every 30 seconds, so try the current one.");
        }
        return View(vm);
    }

    [Authorize, HttpGet]
    public async Task<IActionResult> TwoFactor()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();
        return View(await BuildTwoFactorVm(user));
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorVm vm)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();
        if (!ModelState.IsValid) return View(nameof(TwoFactor), await BuildTwoFactorVm(user));

        var code = vm.Code.Replace(" ", "").Trim();
        var valid = await _users.VerifyTwoFactorTokenAsync(user, _users.Options.Tokens.AuthenticatorTokenProvider, code);
        if (!valid)
        {
            ModelState.AddModelError(nameof(vm.Code), "That code didn't match. Check the key was entered correctly and try the current code.");
            return View(nameof(TwoFactor), await BuildTwoFactorVm(user));
        }

        await _users.SetTwoFactorEnabledAsync(user, true);
        var recovery = await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        await _signIn.RefreshSignInAsync(user); // adds the 2FA claim, so the admin area opens straight away
        _logger.LogInformation("User {UserId} turned on two-step verification", user.Id);

        var fresh = await BuildTwoFactorVm(user);
        fresh.NewRecoveryCodes = recovery?.ToArray();
        TempData["Toast"] = "Two-step verification is on.";
        return View(nameof(TwoFactor), fresh);
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> NewRecoveryCodes()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();
        if (!user.TwoFactorEnabled) return RedirectToAction(nameof(TwoFactor));
        var vm = await BuildTwoFactorVm(user);
        vm.NewRecoveryCodes = (await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))?.ToArray();
        vm.RecoveryCodesLeft = vm.NewRecoveryCodes?.Length ?? 0;
        TempData["Toast"] = "New recovery codes created. The old ones no longer work.";
        return View(nameof(TwoFactor), vm);
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> DisableTwoFactor()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();
        if (await IsTwoFactorRequiredFor(user))
        {
            TempData["Toast"] = "Administrators must keep two-step verification on.";
            return RedirectToAction(nameof(TwoFactor));
        }
        await _users.SetTwoFactorEnabledAsync(user, false);
        await _users.ResetAuthenticatorKeyAsync(user); // an old phone can't be used if it is turned on again
        await _signIn.RefreshSignInAsync(user);
        _logger.LogInformation("User {UserId} turned off two-step verification", user.Id);
        TempData["Toast"] = "Two-step verification is off.";
        return RedirectToAction(nameof(TwoFactor));
    }

    private async Task<TwoFactorVm> BuildTwoFactorVm(ApplicationUser user)
    {
        var key = await _users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _users.ResetAuthenticatorKeyAsync(user);
            key = await _users.GetAuthenticatorKeyAsync(user) ?? "";
        }
        return new TwoFactorVm
        {
            IsEnabled = user.TwoFactorEnabled,
            IsRequired = await IsTwoFactorRequiredFor(user),
            SharedKey = FormatKey(key),
            AuthenticatorUri = string.Format("otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
                UrlEncoder.Default.Encode("PawConnect"), UrlEncoder.Default.Encode(user.Email ?? ""), key),
            RecoveryCodesLeft = user.TwoFactorEnabled ? await _users.CountRecoveryCodesAsync(user) : 0
        };
    }

    private async Task<bool> IsTwoFactorRequiredFor(ApplicationUser user) =>
        RequireAdminTwoFactorFilter.IsRequired(_config, _env) && await _users.IsInRoleAsync(user, Roles.Administrator);

    private static string FormatKey(string key)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
            sb.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        return sb.ToString().TrimEnd().ToLowerInvariant();
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl) => View(new RegisterVm { ReturnUrl = returnUrl });

    [HttpPost, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var now = _clock.UtcNow;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = vm.Email.Trim(), Email = vm.Email.Trim(),
            FirstName = vm.FirstName.Trim(), LastName = vm.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim(),
            CreatedAt = now, ConsentAcceptedAt = now, IsActive = true
        };
        var result = await _users.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(e.Code.Contains("Password") ? nameof(vm.Password) : "", e.Description);
            return View(vm);
        }

        // Everyone who self-registers is an adopter. Volunteer and admin roles are granted by an administrator.
        await _users.AddToRoleAsync(user, Roles.Adopter);
        await _signIn.SignInAsync(user, isPersistent: false);
        TempData["Toast"] = "Account created. Welcome to PawConnect!";
        return LocalRedirect(SafeReturnUrl(vm.ReturnUrl) ?? "/Home/Browse");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        TempData["Toast"] = "You've been signed out.";
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();

    [Authorize, HttpGet]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();
        return View(await BuildProfileVm(user, ct));
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> Me(MyProfileVm vm, CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();
        if (!ModelState.IsValid)
        {
            var fresh = await BuildProfileVm(user, ct);
            fresh.FirstName = vm.FirstName; fresh.LastName = vm.LastName; fresh.Phone = vm.Phone;
            return View(fresh);
        }
        user.FirstName = vm.FirstName.Trim();
        user.LastName = vm.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim();
        await _users.UpdateAsync(user);
        await _signIn.RefreshSignInAsync(user);
        TempData["Toast"] = "Your details have been updated.";
        return RedirectToAction(nameof(Me));
    }

    /// <summary>POPIA right of access: download everything PawConnect holds about you, as JSON.</summary>
    [Authorize, HttpGet]
    public async Task<IActionResult> ExportMyData(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        var export = new
        {
            exportedAtUtc = _clock.UtcNow,
            account = new { user.FirstName, user.LastName, user.Email, Phone = user.PhoneNumber, user.CreatedAt, user.ConsentAcceptedAt, Roles = await _users.GetRolesAsync(user) },
            applications = (await _applications.GetForAdopterAsync(user.Id, ct)).Select(a => new
            {
                a.ReferenceNumber, Animal = a.Animal?.Name, Status = a.Status.ToString(), a.SubmittedAt,
                a.FullName, a.Phone, a.Email, a.Why, a.HomeType, a.OwnRent, a.OtherPets, a.Notes
            }),
            donations = (await _donations.GetForDonorAsync(user.Id, ct)).Select(d => new
            {
                d.ReceiptNumber, d.Amount, Type = d.Type.ToString(), Status = d.Status.ToString(), d.DonatedAt, Animal = d.Animal?.Name
            }),
            volunteerHours = (await _hours.FindAsync(new HourLogFilter(user.Id), ct)).Select(h => new
            {
                h.Date, h.Hours, h.Activity, Status = h.Status.ToString()
            })
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions { WriteIndented = true });
        return File(json, "application/json", $"pawconnect-my-data-{ShelterTime.Today(_clock):yyyy-MM-dd}.json");
    }

    private async Task<MyProfileVm> BuildProfileVm(ApplicationUser user, CancellationToken ct) => new()
    {
        FirstName = user.FirstName,
        LastName = user.LastName,
        Phone = user.PhoneNumber,
        Email = user.Email ?? "",
        Roles = (await _users.GetRolesAsync(user)).ToList(),
        Donations = await _donations.GetForDonorAsync(user.Id, ct)
    };

    private async Task<string> HomeFor(ApplicationUser user)
    {
        if (await _users.IsInRoleAsync(user, Roles.Administrator)) return "/Admin/Dashboard";
        if (await _users.IsInRoleAsync(user, Roles.Volunteer)) return "/Volunteer/Dashboard";
        return "/";
    }

    /// <summary>Only allow redirects back into this site (prevents open-redirect attacks).</summary>
    private string? SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
}
