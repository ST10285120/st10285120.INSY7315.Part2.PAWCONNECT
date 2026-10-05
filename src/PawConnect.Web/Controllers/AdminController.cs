using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;
using PawConnect.Web.Security;
using PawConnect.Web.ViewModels;

namespace PawConnect.Web.Controllers;

/// <summary>Shelter administrator area: dashboard, animals, volunteers, hours, users, shifts, donations, reports.</summary>
[Authorize(Roles = Roles.Administrator)]
public class AdminController : Controller
{
    private readonly ReportService _reports;
    private readonly AnimalService _animalService;
    private readonly HourLogService _hourService;
    private readonly ShiftService _shiftService;
    private readonly DonationService _donationService;
    private readonly IAnimalRepository _animals;
    private readonly IApplicationRepository _applications;
    private readonly IHourLogRepository _hours;
    private readonly IShiftRepository _shifts;
    private readonly IDonationRepository _donations;
    private readonly IUserRepository _userRepo;
    private readonly IBranchRepository _branches;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;

    public AdminController(ReportService reports, AnimalService animalService, HourLogService hourService, ShiftService shiftService,
        DonationService donationService, IAnimalRepository animals, IApplicationRepository applications, IHourLogRepository hours,
        IShiftRepository shifts, IDonationRepository donations, IUserRepository userRepo, IBranchRepository branches,
        UserManager<ApplicationUser> users, IMemoryCache cache, IClock clock)
    {
        _reports = reports;
        _animalService = animalService;
        _hourService = hourService;
        _shiftService = shiftService;
        _donationService = donationService;
        _animals = animals;
        _applications = applications;
        _hours = hours;
        _shifts = shifts;
        _donations = donations;
        _userRepo = userRepo;
        _branches = branches;
        _users = users;
        _cache = cache;
        _clock = clock;
    }

    public async Task<IActionResult> Dashboard(CancellationToken ct) => View(new AdminDashboardVm
    {
        Stats = await _reports.GetDashboardAsync(ct),
        RecentApplications = await _applications.GetRecentAsync(6, ct),
        MonthlyDonations = await _reports.GetMonthlyDonationsAsync(6, ct)
    });

    // ---------- Animals ----------

    public async Task<IActionResult> Animals(CancellationToken ct) => View(await BuildAnimalsVm(null, ct));

    [HttpPost]
    public async Task<IActionResult> AddAnimal([Bind(Prefix = "NewAnimal")] AnimalForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _animalService.CreateAsync(form.ToInput(), ct);
            if (result.Succeeded)
            {
                InvalidatePublicCache();
                TempData["Toast"] = $"{result.Value!.Name} added to the adoption list. Add a photo below.";
                return RedirectToAction(nameof(EditAnimal), new { id = result.Value.Id });
            }
            ModelState.AddModelError("NewAnimal", result.Error!);
        }
        return View(nameof(Animals), await BuildAnimalsVm(form, ct));
    }

    [HttpGet]
    public async Task<IActionResult> EditAnimal(Guid id, CancellationToken ct)
    {
        var animal = await _animals.GetAsync(id, ct);
        if (animal == null) return NotFound();
        return View(await BuildEditVm(AnimalForm.From(animal), animal.Status, ct));
    }

    [HttpPost]
    public async Task<IActionResult> EditAnimal([Bind(Prefix = "Form")] AnimalForm form, CancellationToken ct)
    {
        if (form.Id is not { } id) return BadRequest();
        var animal = await _animals.GetAsync(id, ct);
        if (animal == null) return NotFound();
        if (ModelState.IsValid)
        {
            var result = await _animalService.UpdateAsync(id, form.ToInput(), ct);
            if (result.Succeeded)
            {
                InvalidatePublicCache();
                TempData["Toast"] = $"{form.Name}'s details were saved.";
                return RedirectToAction(nameof(EditAnimal), new { id });
            }
            ModelState.AddModelError("Form", result.Error!);
        }
        return View(await BuildEditVm(form, animal.Status, ct));
    }

    [HttpPost]
    public async Task<IActionResult> SetAnimalStatus(Guid id, AnimalStatus status, string? returnTo, CancellationToken ct)
    {
        var result = await _animalService.SetStatusAsync(id, status, ct);
        InvalidatePublicCache();
        Toast(result, $"Status updated to {status}.");
        return returnTo == "edit" ? RedirectToAction(nameof(EditAnimal), new { id }) : RedirectToAction(nameof(Animals));
    }

    [HttpPost, RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile? photo, CancellationToken ct)
    {
        if (photo == null || photo.Length == 0)
        {
            Toast(OperationResult.Failure("Please choose a photo to upload."), "");
            return RedirectToAction(nameof(EditAnimal), new { id });
        }
        if (photo.Length > AnimalService.MaxPhotoBytes)
        {
            Toast(OperationResult.Failure("Photos must be 2 MB or smaller."), "");
            return RedirectToAction(nameof(EditAnimal), new { id });
        }
        using var ms = new MemoryStream();
        await photo.CopyToAsync(ms, ct);
        var result = await _animalService.AddPhotoAsync(id, ms.ToArray(), ct);
        InvalidatePublicCache();
        Toast(result, "Photo uploaded.");
        return RedirectToAction(nameof(EditAnimal), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> DeletePhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        Toast(await _animalService.DeletePhotoAsync(id, photoId, ct), "Photo removed.");
        return RedirectToAction(nameof(EditAnimal), new { id });
    }

    // ---------- Volunteers & hours ----------

    public async Task<IActionResult> Volunteers(string sort = "name", CancellationToken ct = default)
    {
        var volunteers = await _userRepo.GetInRoleAsync(Roles.Volunteer, ct);
        var totals = await _hours.ApprovedTotalsByVolunteerAsync(ct);
        var pending = (await _hours.FindAsync(new HourLogFilter(Status: HourLogStatus.Pending), ct))
            .GroupBy(h => h.VolunteerId).ToDictionary(g => g.Key, g => g.Count());

        var rows = volunteers.Select(v => new VolunteerRowVm(v, totals.GetValueOrDefault(v.Id), pending.GetValueOrDefault(v.Id)));
        rows = sort switch
        {
            "hours" => rows.OrderByDescending(r => r.ApprovedHours),
            "joined" => rows.OrderBy(r => r.Volunteer.CreatedAt),
            _ => rows.OrderBy(r => r.Volunteer.FullName)
        };
        ViewBag.Sort = sort;
        return View(rows.ToList());
    }

    public async Task<IActionResult> Hours(Guid? volunteerId, string status = "all", CancellationToken ct = default)
    {
        HourLogStatus? statusFilter = Enum.TryParse<HourLogStatus>(status, true, out var s) ? s : null;
        var volunteers = await _userRepo.GetInRoleAsync(Roles.Volunteer, ct);
        var totals = await _hours.ApprovedTotalsByVolunteerAsync(ct);
        var today = ShelterTime.Today(_clock);

        return View(new HoursAdminVm
        {
            Logs = await _hours.FindAsync(new HourLogFilter(volunteerId, statusFilter), ct),
            Volunteers = volunteers,
            SelectedVolunteerId = volunteerId,
            SelectedStatus = statusFilter?.ToString() ?? "all",
            ApprovedThisMonth = await _hours.SumApprovedAsync(null, new DateOnly(today.Year, today.Month, 1), today, ct),
            PendingCount = await _hours.CountPendingAsync(null, ct),
            ChartData = volunteers.Where(v => totals.GetValueOrDefault(v.Id) > 0)
                .Select(v => (v.FullName, totals[v.Id])).OrderByDescending(x => x.Item2).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> DecideHours(Guid id, string decision, Guid? volunteerId, string? status, CancellationToken ct)
    {
        var approve = decision == "approve";
        Toast(await _hourService.ReviewAsync(id, approve, User.GetUserId(), ct), $"Hour log {(approve ? "approved" : "rejected")}.");
        return RedirectToAction(nameof(Hours), new { volunteerId, status = status ?? "all" });
    }

    // ---------- Users ----------

    public async Task<IActionResult> Users(string filter = "all", CancellationToken ct = default) =>
        View(await BuildUsersVm(filter, null, ct));

    [HttpPost]
    public async Task<IActionResult> CreateUser([Bind(Prefix = "NewUser")] CreateUserForm form, CancellationToken ct)
    {
        if (!Roles.All.Contains(form.Role)) ModelState.AddModelError("NewUser.Role", "Unknown role.");
        if (ModelState.IsValid)
        {
            var branch = await _branches.GetDefaultAsync(ct);
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = form.Email.Trim(), Email = form.Email.Trim(), EmailConfirmed = true,
                FirstName = form.FirstName.Trim(), LastName = form.LastName.Trim(), PhoneNumber = form.Phone,
                VolunteerRole = form.Role == Roles.Volunteer ? form.VolunteerRole : null,
                BranchId = form.Role == Roles.Adopter ? null : branch.Id, CreatedAt = _clock.UtcNow, IsActive = true
            };
            var result = await _users.CreateAsync(user, form.TemporaryPassword);
            if (result.Succeeded)
            {
                await _users.AddToRoleAsync(user, form.Role);
                TempData["Toast"] = $"{user.FullName} can now sign in as a {form.Role.ToLower()}. Share the temporary password with them securely.";
                return RedirectToAction(nameof(Users));
            }
            foreach (var e in result.Errors) ModelState.AddModelError("NewUser", e.Description);
        }
        return View(nameof(Users), await BuildUsersVm("all", form, ct));
    }

    [HttpPost]
    public async Task<IActionResult> SetRole(Guid userId, string role, bool grant, CancellationToken ct)
    {
        if (!Roles.All.Contains(role)) return BadRequest();
        if (userId == User.GetUserId() && role == Roles.Administrator && !grant)
        {
            Toast(OperationResult.Failure("You can't remove your own administrator role."), "");
            return RedirectToAction(nameof(Users));
        }
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user == null) return NotFound();

        var result = grant ? await _users.AddToRoleAsync(user, role) : await _users.RemoveFromRoleAsync(user, role);
        if (result.Succeeded)
        {
            if (grant && role != Roles.Adopter && user.BranchId == null)
            {
                user.BranchId = (await _branches.GetDefaultAsync(ct)).Id;
                await _users.UpdateAsync(user);
            }
            await _users.UpdateSecurityStampAsync(user); // forces their session to pick up the new role
        }
        Toast(result.Succeeded ? OperationResult.Success() : OperationResult.Failure(string.Join(" ", result.Errors.Select(e => e.Description))),
            $"{user.FullName} {(grant ? "is now" : "is no longer")} a {role.ToLower()}.");
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(Guid userId, bool active, CancellationToken ct)
    {
        if (userId == User.GetUserId())
        {
            Toast(OperationResult.Failure("You can't deactivate your own account."), "");
            return RedirectToAction(nameof(Users));
        }
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user == null) return NotFound();

        user.IsActive = active;
        await _users.UpdateAsync(user);
        // Lock out deactivated accounts and invalidate any session they already have.
        await _users.SetLockoutEnabledAsync(user, true);
        await _users.SetLockoutEndDateAsync(user, active ? null : DateTimeOffset.MaxValue);
        await _users.UpdateSecurityStampAsync(user);
        TempData["Toast"] = $"{user.FullName} has been {(active ? "reactivated" : "deactivated")}.";
        return RedirectToAction(nameof(Users));
    }

    // ---------- Shifts ----------

    public async Task<IActionResult> Shifts(CancellationToken ct) => View(await BuildShiftsVm(null, ct));

    [HttpPost]
    public async Task<IActionResult> CreateShift([Bind(Prefix = "NewShift")] ShiftForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _shiftService.CreateAsync(new ShiftInput(
                form.Date.ToDateTime(form.StartTime), form.Date.ToDateTime(form.EndTime), form.Role, form.Capacity, form.Notes, null), ct);
            if (result.Succeeded)
            {
                TempData["Toast"] = "Shift created. Volunteers can sign up now.";
                return RedirectToAction(nameof(Shifts));
            }
            ModelState.AddModelError("NewShift", result.Error!);
        }
        return View(nameof(Shifts), await BuildShiftsVm(form, ct));
    }

    // ---------- Donations ----------

    public async Task<IActionResult> Donations(CancellationToken ct)
    {
        var today = ShelterTime.Today(_clock);
        var (fromUtc, toUtc) = ReportService.ToUtcRange(today.AddDays(-30), today);
        var received = await _donations.GetBetweenAsync(fromUtc, toUtc, DonationStatus.Received, ct);
        return View(new DonationsAdminVm
        {
            Pledged = await _donations.GetPledgedAsync(ct),
            RecentlyReceived = received.OrderByDescending(d => d.ReceivedAt).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> MarkDonationReceived(Guid id, CancellationToken ct)
    {
        Toast(await _donationService.MarkReceivedAsync(id, User.GetUserId(), ct), "Donation marked as received and a receipt emailed to the donor.");
        InvalidatePublicCache();
        return RedirectToAction(nameof(Donations));
    }

    [HttpPost]
    public async Task<IActionResult> CancelDonation(Guid id, CancellationToken ct)
    {
        Toast(await _donationService.CancelAsync(id, User.GetUserId(), ct), "Pledge cancelled.");
        return RedirectToAction(nameof(Donations));
    }

    // ---------- Reports ----------

    public async Task<IActionResult> Reports(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var today = ShelterTime.Today(_clock);
        var f = from ?? new DateOnly(today.Year, today.Month, 1).AddMonths(-2);
        var t = to ?? today;
        var result = await _reports.GetSummaryAsync(f, t, ct);
        return View(new ReportVm { From = f, To = t, Summary = result.Value, Error = result.Error });
    }

    /// <summary>Exports donations for a date range as CSV (administrator story: funding applications).</summary>
    public async Task<IActionResult> ExportDonations(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var result = await _reports.GetSummaryAsync(from, to, ct);
        if (!result.Succeeded) return BadRequest(result.Error);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ReportService.ToCsv(result.Value!.Donations))).ToArray();
        return File(bytes, "text/csv", $"pawconnect-donations-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.csv");
    }

    // ---------- helpers ----------

    private void Toast(OperationResult result, string success)
    {
        TempData["Toast"] = result.Succeeded ? success : result.Error;
        if (!result.Succeeded) TempData["ToastKind"] = "error";
    }

    private void InvalidatePublicCache() => _cache.Remove("home-stats");

    private async Task<AdminAnimalsVm> BuildAnimalsVm(AnimalForm? form, CancellationToken ct) => new()
    {
        Animals = await _animals.SearchAsync(new AnimalSearch(), ct),
        NewAnimal = form ?? new AnimalForm { IntakeDate = ShelterTime.Today(_clock) },
        Branches = await _branches.GetAllAsync(ct)
    };

    private async Task<EditAnimalVm> BuildEditVm(AnimalForm form, AnimalStatus status, CancellationToken ct) => new()
    {
        Form = form,
        Status = status,
        Photos = await _animals.GetPhotoInfosAsync(form.Id!.Value, ct),
        Branches = await _branches.GetAllAsync(ct)
    };

    private async Task<UsersAdminVm> BuildUsersVm(string filter, CreateUserForm? form, CancellationToken ct)
    {
        var roles = await _userRepo.GetRolesByUserAsync(ct);
        var rows = (await _userRepo.GetAllAsync(ct))
            .Select(u => new UserRowVm(u, roles.GetValueOrDefault(u.Id) ?? new List<string>()))
            .Where(r => filter == "all" || r.Roles.Contains(filter))
            .ToList();
        return new UsersAdminVm
        {
            Users = rows, Filter = filter, NewUser = form ?? new CreateUserForm(),
            CurrentUserId = User.GetUserId(), Branches = await _branches.GetAllAsync(ct)
        };
    }

    private async Task<ShiftsAdminVm> BuildShiftsVm(ShiftForm? form, CancellationToken ct) => new()
    {
        Shifts = await _shifts.GetUpcomingAsync(_clock.UtcNow.AddDays(-1), null, ct),
        NewShift = form ?? new ShiftForm { Date = ShelterTime.Today(_clock).AddDays(1) }
    };
}
