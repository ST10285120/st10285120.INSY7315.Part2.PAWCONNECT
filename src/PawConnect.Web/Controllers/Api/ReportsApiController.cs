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

[Route("api/reports")]
[Authorize(AuthenticationSchemes = ApiAuth.Schemes, Roles = Roles.Administrator)]
public class ReportsApiController : ApiControllerBase
{
    private readonly ReportService _reports;
    public ReportsApiController(ReportService reports) => _reports = reports;

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery, BindRequired] DateOnly from, [FromQuery, BindRequired] DateOnly to, CancellationToken ct)
    {
        var result = await _reports.GetSummaryAsync(from, to, ct);
        if (!result.Succeeded) return ProblemFrom(result);
        var s = result.Value!;
        return Ok(new
        {
            s.From, s.To, s.DonationsReceived, s.DonationCount, s.GeneralDonations, s.Sponsorships, s.AdoptionFees,
            s.OutstandingPledges, s.ApplicationsSubmitted, s.Adoptions, s.AverageDecisionHours, s.VolunteerHours
        });
    }

    [HttpGet("donations.csv"), Produces("text/csv")]
    public async Task<IActionResult> DonationsCsv([FromQuery, BindRequired] DateOnly from, [FromQuery, BindRequired] DateOnly to, CancellationToken ct)
    {
        var result = await _reports.GetSummaryAsync(from, to, ct);
        if (!result.Succeeded) return ProblemFrom(result);
        return File(System.Text.Encoding.UTF8.GetBytes(ReportService.ToCsv(result.Value!.Donations)), "text/csv", $"donations-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");
    }
}
