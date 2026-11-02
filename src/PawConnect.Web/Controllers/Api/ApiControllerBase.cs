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

/// <summary>
/// Base class for the REST API. [ApiController] gives automatic model validation (400 with
/// ProblemDetails). ApiAntiforgeryFilter protects cookie-authenticated calls from CSRF.
/// </summary>
[ApiController]
[IgnoreAntiforgeryToken(Order = 1001)] // replaced by ApiAntiforgeryFilter below
[ServiceFilter(typeof(ApiAntiforgeryFilter))]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Turns a failed business result into RFC 7807 problem details with the matching status code.</summary>
    protected ActionResult ProblemFrom(OperationResult result) =>
        Problem(title: result.Error, statusCode: result.ErrorKind switch
        {
            OperationErrorKind.NotFound => StatusCodes.Status404NotFound,
            OperationErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        });
}
