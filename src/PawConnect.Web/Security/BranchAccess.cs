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
/// Branch scoping for volunteers (traceability V1): a volunteer only sees and acts on adoption
/// applications for animals at their own branch. Administrators, and volunteers not yet assigned
/// to a branch, work across all branches. Used by both the MVC pages and the REST API, so the
/// rule is enforced server-side, not just by what the dashboard lists.
/// </summary>
public class BranchAccess
{
    private readonly PawConnect.Core.Interfaces.IUserRepository _users;
    public BranchAccess(PawConnect.Core.Interfaces.IUserRepository users) => _users = users;

    /// <summary>The branch to filter by, or null for "all branches".</summary>
    public async Task<Guid?> BranchFilterAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (user.IsInRole(Roles.Administrator)) return null;
        return (await _users.GetAsync(user.GetUserId(), ct))?.BranchId;
    }

    public async Task<bool> CanReviewAsync(ClaimsPrincipal user, PawConnect.Core.Entities.AdoptionApplication app, CancellationToken ct = default)
    {
        if (!user.IsStaff()) return false;
        var branch = await BranchFilterAsync(user, ct);
        return branch == null || app.Animal?.BranchId == branch;
    }
}
