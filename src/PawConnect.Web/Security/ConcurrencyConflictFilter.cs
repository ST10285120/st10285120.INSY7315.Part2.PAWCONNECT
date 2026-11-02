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
