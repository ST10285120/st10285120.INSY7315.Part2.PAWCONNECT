using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Core.Common;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;
using PawConnect.Web.Security;
using PawConnect.Web.ViewModels;

namespace PawConnect.Web.Controllers;

/// <summary>Volunteer (and admin) workspace: review applications, sign up for shifts, log hours.</summary>
[Authorize(Roles = Roles.Staff)]
public class VolunteerController : Controller
{
    private readonly IApplicationRepository _applications;
    private readonly IShiftRepository _shifts;
    private readonly IHourLogRepository _hours;
    private readonly IUserRepository _users;
    private readonly IDonationRepository _donations;
    private readonly AdoptionService _adoptions;
    private readonly DonationService _donationService;
    private readonly ShiftService _shiftService;
    private readonly HourLogService _hourService;
    private readonly IClock _clock;
    private readonly BranchAccess _access;

    public VolunteerController(IApplicationRepository applications, IShiftRepository shifts, IHourLogRepository hours,
        IUserRepository users, IDonationRepository donations, AdoptionService adoptions, DonationService donationService,
        ShiftService shiftService, HourLogService hourService, IClock clock, BranchAccess access)
    {
        _access = access;
        _applications = applications;
        _shifts = shifts;
        _hours = hours;
        _users = users;
        _donations = donations;
        _adoptions = adoptions;
        _donationService = donationService;
        _shiftService = shiftService;
        _hourService = hourService;
        _clock = clock;
    }

    public async Task<IActionResult> Dashboard(CancellationToken ct) => View(await BuildDashboard(null, ct));

    // ----- Applications -----

    public async Task<IActionResult> Application(Guid id, CancellationToken ct)
    {
        var app = await _applications.GetAsync(id, ct);
        // 404 rather than 403 for another branch's application, so ids can't be probed.
        if (app == null || !await _access.CanReviewAsync(User, app, ct)) return NotFound();
        return View(new ApplicationDetailVm
        {
            Application = app,
            FeeRecorded = await _donations.HasReceivedAdoptionFeeAsync(id, ct),
            IsAdmin = User.IsInRole(Roles.Administrator)
        });
    }

    [HttpPost]
    public async Task<IActionResult> StartReview(Guid id, CancellationToken ct) =>
        await InScopeAsync(id, ct) ? Done(await _adoptions.StartReviewAsync(id, User.GetUserId(), ct), id, "Application moved to Under Review.") : NotFound();

    [HttpPost]
    public async Task<IActionResult> Approve(Guid id, string? note, CancellationToken ct) =>
        await InScopeAsync(id, ct) ? Done(await _adoptions.ApproveAsync(id, User.GetUserId(), note, ct), id, "Application approved. The applicant has been notified.") : NotFound();

    [HttpPost]
    public async Task<IActionResult> Reject(Guid id, string? note, CancellationToken ct) =>
        await InScopeAsync(id, ct) ? Done(await _adoptions.RejectAsync(id, User.GetUserId(), note, ct), id, "Application rejected. The applicant has been notified.") : NotFound();

    [HttpPost]
    public async Task<IActionResult> HomeVisit(Guid id, string? notes, CancellationToken ct) =>
        await InScopeAsync(id, ct) ? Done(await _adoptions.RecordHomeVisitAsync(id, User.GetUserId(), notes, ct), id, "Home visit recorded.") : NotFound();

    [HttpPost]
    public async Task<IActionResult> AdoptionFee(Guid id, decimal amount, CancellationToken ct) =>
        await InScopeAsync(id, ct) ? Done(await _donationService.RecordAdoptionFeeAsync(id, amount, User.GetUserId(), ct), id, "Adoption fee recorded.") : NotFound();

    [HttpPost]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct) =>
        await InScopeAsync(id, ct) ? Done(await _adoptions.CompleteAdoptionAsync(id, User.GetUserId(), ct), id, "Adoption completed. 🎉") : NotFound();

    private async Task<bool> InScopeAsync(Guid applicationId, CancellationToken ct) =>
        await _applications.GetAsync(applicationId, ct) is { } app && await _access.CanReviewAsync(User, app, ct);

    private IActionResult Done(OperationResult result, Guid id, string success)
    {
        TempData["Toast"] = result.Succeeded ? success : result.Error;
        if (!result.Succeeded) TempData["ToastKind"] = "error";
        return RedirectToAction(nameof(Application), new { id });
    }

    // ----- Shifts -----

    [HttpPost]
    public async Task<IActionResult> SignUpShift(Guid id, CancellationToken ct)
    {
        var result = await _shiftService.SignUpAsync(id, User.GetUserId(), ct);
        TempData["Toast"] = result.Succeeded ? "You're signed up for that shift. A confirmation is on its way." : result.Error;
        if (!result.Succeeded) TempData["ToastKind"] = "error";
        return RedirectToAction(nameof(Dashboard), null, "shifts");
    }

    [HttpPost]
    public async Task<IActionResult> CancelShift(Guid id, CancellationToken ct)
    {
        var result = await _shiftService.CancelAsync(id, User.GetUserId(), ct);
        TempData["Toast"] = result.Succeeded ? "You've been removed from that shift." : result.Error;
        if (!result.Succeeded) TempData["ToastKind"] = "error";
        return RedirectToAction(nameof(Dashboard), null, "shifts");
    }

    // ----- Hours -----

    [HttpPost]
    public async Task<IActionResult> LogHours([Bind(Prefix = "HourForm")] HourLogForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _hourService.LogAsync(User.GetUserId(), new HourLogInput(form.Date, form.Hours, form.Activity, form.Notes), ct);
            if (result.Succeeded)
            {
                TempData["Toast"] = $"Logged {form.Hours:0.##}h of {form.Activity}. It's pending admin approval.";
                return RedirectToAction(nameof(Dashboard), null, "hours");
            }
            ModelState.AddModelError("HourForm", result.Error!);
        }
        return View(nameof(Dashboard), await BuildDashboard(form, ct));
    }

    private async Task<VolunteerDashboardVm> BuildDashboard(HourLogForm? form, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var me = await _users.GetAsync(userId, ct);
        var isAdmin = User.IsInRole(Roles.Administrator);
        // Volunteers see applications for their own branch; administrators see every branch.
        var branchFilter = isAdmin ? null : me?.BranchId;

        var shifts = await _shifts.GetUpcomingAsync(_clock.UtcNow, branchFilter, ct);
        return new VolunteerDashboardVm
        {
            FirstName = me?.FirstName ?? "",
            IsAdmin = isAdmin,
            OpenApplications = await _applications.GetOpenForReviewAsync(branchFilter, ct),
            Shifts = shifts.Select(s => new ShiftRowVm(s, s.Signups.Any(x => x.VolunteerId == userId))).ToList(),
            MyHours = await _hours.FindAsync(new HourLogFilter(userId), ct),
            MyApprovedTotal = await _hours.SumApprovedAsync(userId, null, null, ct),
            HourForm = form ?? new HourLogForm { Date = ShelterTime.Today(_clock) }
        };
    }
}
