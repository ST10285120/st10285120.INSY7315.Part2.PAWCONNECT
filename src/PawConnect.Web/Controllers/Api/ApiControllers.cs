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

[Route("api/auth")]
public class AuthApiController : ApiControllerBase
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;

    public AuthApiController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users)
    {
        _signIn = signIn;
        _users = users;
    }

    /// <summary>Exchanges email + password for a bearer token (for API clients and testing).</summary>
    [HttpPost("token"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Token(TokenRequest request)
    {
        // Check the password before revealing that an account is deactivated, so the API can't be
        // used to find out which email addresses have (deactivated) accounts.
        var user = await _users.FindByEmailAsync(request.Email);
        if (user is { IsActive: false } && !await _users.IsLockedOutAsync(user))
        {
            var passwordOk = await _users.CheckPasswordAsync(user, request.Password);
            if (!passwordOk) await _users.AccessFailedAsync(user); // wrong guesses still count toward lockout
            return Problem(title: passwordOk ? "Account deactivated. Contact the shelter." : "Incorrect email or password.", statusCode: 401);
        }

        _signIn.AuthenticationScheme = IdentityConstants.BearerScheme;
        var result = await _signIn.PasswordSignInAsync(request.Email, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Problem(title: result.IsLockedOut ? "Account locked. Try again in 15 minutes." : "Incorrect email or password.", statusCode: 401);
        return new EmptyResult(); // the bearer handler has already written { accessToken, expiresIn, refreshToken }
    }
}

[Route("api/animals")]
public class AnimalsApiController : ApiControllerBase
{
    private readonly IAnimalRepository _animals;
    private readonly IMedicalRecordRepository _records;
    private readonly AnimalService _service;
    private readonly IClock _clock;

    public AnimalsApiController(IAnimalRepository animals, IMedicalRecordRepository records, AnimalService service, IClock clock)
    {
        _animals = animals;
        _records = records;
        _service = service;
        _clock = clock;
    }

    /// <summary>Public list of animals with the same filters as the Browse page.</summary>
    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<List<AnimalDto>>> List([FromQuery] Species? species, [FromQuery] AnimalSize? size,
        [FromQuery] bool available = false, [FromQuery] bool kids = false, [FromQuery] bool pets = false,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        if (search?.Length > 100) search = search[..100];
        var animals = await _animals.SearchAsync(new AnimalSearch(species, size, available, kids ? true : null, pets ? true : null, search), ct);
        var photos = await _animals.GetPrimaryPhotoIdsAsync(animals.Select(a => a.Id), ct);
        var today = ShelterTime.Today(_clock);
        return animals.Select(a => AnimalDto.From(a, photos.TryGetValue(a.Id, out var p) ? p : null, today)).ToList();
    }

    /// <summary>
    /// GET /api/animals/{id}?include=history (design doc, section 4.2). The medical history is only
    /// returned to volunteers and administrators; the public gets the profile.
    /// </summary>
    [HttpGet("{id:guid}"), AllowAnonymous]
    public async Task<ActionResult<AnimalDetailDto>> Get(Guid id, [FromQuery] string? include, CancellationToken ct)
    {
        var animal = await _animals.GetAsync(id, ct);
        if (animal == null) return NotFound();
        var photos = await _animals.GetPrimaryPhotoIdsAsync(new[] { id }, ct);
        var dto = AnimalDto.From(animal, photos.TryGetValue(id, out var p) ? p : null, ShelterTime.Today(_clock));

        List<MedicalRecordDto>? history = null;
        if (string.Equals(include, "history", StringComparison.OrdinalIgnoreCase))
        {
            // This action allows anonymous access, so sign-in is checked by hand: cookie first, then bearer token.
            System.Security.Claims.ClaimsPrincipal? caller = null;
            foreach (var scheme in new[] { IdentityConstants.ApplicationScheme, IdentityConstants.BearerScheme })
            {
                var auth = await HttpContext.AuthenticateAsync(scheme);
                if (auth.Succeeded) { caller = auth.Principal; break; }
            }
            if (caller == null) return Unauthorized();
            if (!caller.IsStaff()) return Forbid(ApiAuth.Schemes.Split(','));
            history = (await _records.GetForAnimalAsync(id, ct)).Select(MedicalRecordDto.From).ToList();
        }
        return new AnimalDetailDto(dto, history);
    }

    [HttpPatch("{id:guid}/status"), Authorize(AuthenticationSchemes = ApiAuth.Schemes, Roles = Roles.Administrator)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] AnimalStatus status, CancellationToken ct)
    {
        var result = await _service.SetStatusAsync(id, status, ct);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }
}

[Route("api/medical-records")]
[Authorize(AuthenticationSchemes = ApiAuth.Schemes, Roles = Roles.Staff)]
public class MedicalRecordsApiController : ApiControllerBase
{
    private readonly MedicalRecordService _service;
    private readonly IMedicalRecordRepository _records;

    public MedicalRecordsApiController(MedicalRecordService service, IMedicalRecordRepository records)
    {
        _service = service;
        _records = records;
    }

    /// <summary>POST /api/medical-records: only volunteers and administrators can write medical records.</summary>
    [HttpPost]
    public async Task<ActionResult<MedicalRecordDto>> Create(CreateMedicalRecordRequest request, CancellationToken ct)
    {
        var result = await _service.AddAsync(request.AnimalId, User.GetUserId(), request.ToInput(), ct);
        if (!result.Succeeded) return ProblemFrom(result);
        var saved = (await _records.GetAsync(result.Value!.Id, ct))!;
        return CreatedAtAction(nameof(Get), new { id = saved.Id }, MedicalRecordDto.From(saved));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MedicalRecordDto>> Get(Guid id, CancellationToken ct)
    {
        var record = await _records.GetAsync(id, ct);
        return record == null ? NotFound() : MedicalRecordDto.From(record);
    }
}

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

[Route("api/shifts")]
[Authorize(AuthenticationSchemes = ApiAuth.Schemes, Roles = Roles.Staff)]
public class ShiftsApiController : ApiControllerBase
{
    private readonly ShiftService _service;
    private readonly IShiftRepository _shifts;
    private readonly IClock _clock;
    private readonly BranchAccess _access;

    public ShiftsApiController(ShiftService service, IShiftRepository shifts, IClock clock, BranchAccess access)
    {
        _service = service;
        _shifts = shifts;
        _clock = clock;
        _access = access;
    }

    [HttpGet]
    public async Task<ActionResult<List<ShiftDto>>> Upcoming([FromQuery] bool openOnly = false, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var shifts = await _shifts.GetUpcomingAsync(_clock.UtcNow, await _access.BranchFilterAsync(User, ct), ct);
        return shifts.Where(s => !openOnly || !s.IsFull).Select(s => ShiftDto.From(s, userId)).ToList();
    }

    [HttpPost("{id:guid}/signup")]
    public async Task<IActionResult> SignUp(Guid id, CancellationToken ct)
    {
        var result = await _service.SignUpAsync(id, User.GetUserId(), ct);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }

    [HttpDelete("{id:guid}/signup")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _service.CancelAsync(id, User.GetUserId(), ct);
        return result.Succeeded ? NoContent() : ProblemFrom(result);
    }
}

[Route("api/donations")]
[Authorize(AuthenticationSchemes = ApiAuth.Schemes)]
public class DonationsApiController : ApiControllerBase
{
    private readonly DonationService _service;
    private readonly IDonationRepository _donations;

    public DonationsApiController(DonationService service, IDonationRepository donations)
    {
        _service = service;
        _donations = donations;
    }

    [HttpPost]
    public async Task<ActionResult<DonationDto>> Pledge(DonationRequest request, CancellationToken ct)
    {
        var result = await _service.PledgeAsync(User.GetUserId(), new DonationPledge(request.Amount, request.Frequency, request.SponsorAnimalId, request.Note), ct);
        if (!result.Succeeded) return ProblemFrom(result);
        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, DonationDto.From(result.Value));
    }

    /// <summary>One donation: only its donor or staff can read it.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DonationDto>> Get(Guid id, CancellationToken ct)
    {
        var donation = await _donations.GetAsync(id, ct);
        if (donation == null || (!User.IsStaff() && donation.DonorId != User.GetUserId())) return NotFound();
        return DonationDto.From(donation);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<DonationDto>>> Mine(CancellationToken ct) =>
        (await _donations.GetForDonorAsync(User.GetUserId(), ct)).Select(DonationDto.From).ToList();
}

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
