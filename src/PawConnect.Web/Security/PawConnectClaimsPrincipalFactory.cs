using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PawConnect.Core.Common;
using PawConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace PawConnect.Web.Security;

/// <summary>
/// Adds the user's first name (so the layout can greet them) and whether two-step verification is on
/// (for RequireAdminTwoFactorFilter) as claims, so neither needs a database call per request.
/// </summary>
public class PawConnectClaimsPrincipalFactory : UserClaimsPrincipalFactory<PawConnect.Core.Entities.ApplicationUser, IdentityRole<Guid>>
{
    public PawConnectClaimsPrincipalFactory(UserManager<PawConnect.Core.Entities.ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager, Microsoft.Extensions.Options.IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(PawConnect.Core.Entities.ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("given_name", user.FirstName));
        if (user.TwoFactorEnabled) identity.AddClaim(new Claim(TwoFactorClaims.Type, TwoFactorClaims.Enabled));
        return identity;
    }
}
