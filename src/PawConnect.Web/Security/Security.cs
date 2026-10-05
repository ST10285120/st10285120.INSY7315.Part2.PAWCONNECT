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

/// <summary>
/// The API accepts either the browser's login cookie (used by the site's own JavaScript) or a
/// bearer token from POST /api/auth/token (used by other clients, e.g. the .http test file).
/// </summary>
public static class ApiAuth
{
    public const string Schemes = "Identity.Application,Identity.Bearer";
}

/// <summary>
/// CSRF protection for the API. Requests authenticated with the login cookie must send the
/// antiforgery token in the X-CSRF-TOKEN header. Bearer-token requests are not exposed to CSRF,
/// because browsers never attach bearer tokens automatically, so they are allowed through.
/// </summary>
public class ApiAntiforgeryFilter : IAsyncAuthorizationFilter
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };
    private readonly IAntiforgery _antiforgery;

    public ApiAntiforgeryFilter(IAntiforgery antiforgery) => _antiforgery = antiforgery;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (SafeMethods.Contains(request.Method)) return;
        var auth = request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return;
        if (!request.Cookies.Keys.Any(k => k.StartsWith(".AspNetCore.Identity", StringComparison.Ordinal))) return; // anonymous

        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Missing or invalid anti-forgery token.",
                Detail = "Send the token from the page's <meta name=\"csrf-token\"> in the X-CSRF-TOKEN header."
            }) { StatusCode = StatusCodes.Status400BadRequest };
        }
    }
}

/// <summary>Adds standard browser security headers to every response (OWASP A05).</summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task Invoke(HttpContext context)
    {
        var h = context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        // Scripts only from our own origin (no inline script), fonts from Google Fonts.
        h["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; " +
            "frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'";
        return _next(context);
    }
}

/// <summary>Health check used by the deployment pipeline's smoke test and slot-swap check.</summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;
    public DatabaseHealthCheck(AppDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            if (_db.Database.IsRelational())
            {
                // A pooled "SELECT 1" is much cheaper than CanConnectAsync, which opens an unpooled connection.
                await _db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
                return HealthCheckResult.Healthy("Database reachable");
            }
            return await _db.Database.CanConnectAsync(ct)
                ? HealthCheckResult.Healthy("Database reachable")
                : HealthCheckResult.Unhealthy("Database not reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed", ex);
        }
    }
}

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

/// <summary>
/// Safety net for optimistic-concurrency conflicts a service didn't handle itself: the API answers
/// 409 problem details and pages show a friendly "someone else changed this" message, instead of
/// an error page. Nothing was saved, because the conflict rolled the whole change back.
/// </summary>
public class ConcurrencyConflictFilter : IExceptionFilter
{
    private readonly Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory _tempData;
    private readonly Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory _problems;

    public ConcurrencyConflictFilter(Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory tempData,
        Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory problems)
    {
        _tempData = tempData;
        _problems = problems;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not PawConnect.Core.Interfaces.ConcurrencyConflictException) return;
        const string message = "Someone else changed this a moment ago. Please check the latest details and try again.";
        var http = context.HttpContext;
        if (http.Request.Path.StartsWithSegments("/api"))
        {
            var problem = _problems.CreateProblemDetails(http, StatusCodes.Status409Conflict, title: message);
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict, ContentTypes = { "application/problem+json" } };
        }
        else
        {
            var tempData = _tempData.GetTempData(http);
            tempData["Toast"] = message;
            tempData["ToastKind"] = "error";
            var referer = http.Request.Headers.Referer.ToString();
            var back = Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Host == http.Request.Host.Host
                ? uri.PathAndQuery : "/";
            context.Result = new LocalRedirectResult(back);
        }
        context.ExceptionHandled = true;
    }
}
