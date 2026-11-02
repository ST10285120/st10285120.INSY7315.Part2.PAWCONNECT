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
