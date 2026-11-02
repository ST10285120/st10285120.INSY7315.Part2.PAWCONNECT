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

/// <summary>Adds the user's first name as a claim so the layout can greet them without a database call.</summary>
public class PawConnectClaimsPrincipalFactory : UserClaimsPrincipalFactory<PawConnect.Core.Entities.ApplicationUser, IdentityRole<Guid>>
{
    public PawConnectClaimsPrincipalFactory(UserManager<PawConnect.Core.Entities.ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager, Microsoft.Extensions.Options.IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(PawConnect.Core.Entities.ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("given_name", user.FirstName));
        return identity;
    }
}
