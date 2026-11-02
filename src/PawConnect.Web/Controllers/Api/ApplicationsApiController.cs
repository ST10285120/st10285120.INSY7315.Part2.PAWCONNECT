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

[Route("api/applications")]
[Authorize(AuthenticationSchemes = ApiAuth.Schemes)]
public class ApplicationsApiController : ApiControllerBase
{
    private readonly AdoptionService _service;
    private readonly IApplicationRepository _applications;
    private readonly BranchAccess _access;

    public ApplicationsApiController(AdoptionService service, IApplicationRepository applications, BranchAccess access)
    {
        _service = service;
        _applications = applications;
        _access = access;
    }

    /// <summary>Adopters see their own applications; volunteers their branch's; admins all.</summary>
    private async Task<bool> CanSeeAsync(AdoptionApplication app, CancellationToken ct) =>
        app.AdopterId == User.GetUserId() || await _access.CanReviewAsync(User, app, ct);

    [HttpGet("mine"), Authorize(AuthenticationSchemes = ApiAuth.Schemes, Roles = Roles.Adopter)]
    public async Task<ActionResult<List<ApplicationDto>>> Mine(CancellationToken ct) =>
        (await _applications.GetForAdopterAsync(User.GetUserId(), ct)).Select(ApplicationDto.From).ToList();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApplicationDto>> Get(Guid id, CancellationToken ct)
    {
        var app = await _applications.GetAsync(id, ct);
        // Server-side ownership / branch check; 404 (not 403) so ids can't be probed.
        if (app == null || !await CanSeeAsync(app, ct)) return NotFound();
        return ApplicationDto.From(app);
    }

    [HttpPost, Authorize(AuthenticationSchemes = ApiAuth.Schemes, Roles = Roles.Adopter)]
    public async Task<ActionResult<ApplicationDto>> Submit(SubmitApplicationRequest request, CancellationToken ct)
    {
        var result = await _service.SubmitAsync(User.GetUserId(), request.ToSubmission(), ct);
        if (!result.Succeeded) return ProblemFrom(result);
        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, ApplicationDto.From(result.Value));
    }

    /// <summary>Moves an application through the state machine (staff only).</summary>
    [HttpPost("{id:guid}/transitions"), Authorize(AuthenticationSchemes = ApiAuth.Schemes, Roles = Roles.Staff)]
    public async Task<IActionResult> Transition(Guid id, TransitionRequest request, CancellationToken ct)
    {
        var app = await _applications.GetAsync(id, ct);
        if (app == null || !await _access.CanReviewAsync(User, app, ct))
            return Problem(title: "Application not found.", statusCode: StatusCodes.Status404NotFound);
        var staff = User.GetUserId();
        var result = request.Status switch
        {
            ApplicationStatus.UnderReview => await _service.StartReviewAsync(id, staff, ct),
            ApplicationStatus.Approved => await _service.ApproveAsync(id, staff, request.Note, ct),
            ApplicationStatus.Rejected => await _service.RejectAsync(id, staff, request.Note, ct),
            ApplicationStatus.Adopted => await _service.CompleteAdoptionAsync(id, staff, ct),
            _ => OperationResult.Failure("Use the withdraw endpoint to withdraw an application.")
        };
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpPost("{id:guid}/withdraw"), Authorize(AuthenticationSchemes = ApiAuth.Schemes, Roles = Roles.Adopter)]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        var result = await _service.WithdrawAsync(id, User.GetUserId(), ct);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }
}
