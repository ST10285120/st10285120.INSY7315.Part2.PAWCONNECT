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

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("The current user has no id claim.");

    public static bool IsStaff(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Volunteer) || user.IsInRole(Roles.Administrator);
}
