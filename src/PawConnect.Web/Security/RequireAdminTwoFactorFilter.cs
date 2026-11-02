using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PawConnect.Core.Common;

namespace PawConnect.Web.Security;

/// <summary>
/// Administrators must turn on two-step verification before they can use the admin area
/// (Task 2 feedback: add MFA for admin accounts). An admin without it is sent to the setup page.
/// On by default in Production; Security:RequireAdminTwoFactor overrides it (staging keeps it off
/// so the shared demo admin account still works for markers and the client).
/// </summary>
public class RequireAdminTwoFactorFilter : IAsyncActionFilter
{
    private readonly bool _required;

    public RequireAdminTwoFactorFilter(IConfiguration config, IWebHostEnvironment env) =>
        _required = IsRequired(config, env);

    public static bool IsRequired(IConfiguration config, IWebHostEnvironment env) =>
        config.GetValue("Security:RequireAdminTwoFactor", env.IsProduction());

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (_required && user.IsInRole(Roles.Administrator) && !user.HasClaim(TwoFactorClaims.Type, TwoFactorClaims.Enabled))
        {
            if (context.Controller is Controller controller)
                controller.TempData["Toast"] = "Please set up two-step verification to use the admin area.";
            context.Result = new RedirectToActionResult("TwoFactor", "Account", new { required = true });
            return Task.CompletedTask;
        }
        return next();
    }
}
