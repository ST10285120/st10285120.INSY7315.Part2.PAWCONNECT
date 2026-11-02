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
/// The API accepts either the browser's login cookie (used by the site's own JavaScript) or a
/// bearer token from POST /api/auth/token (used by other clients, e.g. the .http test file).
/// </summary>
public static class ApiAuth
{
    public const string Schemes = "Identity.Application,Identity.Bearer";
}
