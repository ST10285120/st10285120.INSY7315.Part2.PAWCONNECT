using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PawConnect.Core.Common;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;
using PawConnect.Web.Security;
using PawConnect.Web.ViewModels;

namespace PawConnect.Web.Controllers;

public class HomeController : Controller
{
    private readonly IAnimalRepository _animals;
    private readonly IApplicationRepository _applications;
    private readonly ReportService _reports;
    private readonly DonationService _donations;
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;

    public HomeController(IAnimalRepository animals, IApplicationRepository applications, ReportService reports,
        DonationService donations, IMemoryCache cache, IClock clock)
    {
        _animals = animals;
        _applications = applications;
        _reports = reports;
        _donations = donations;
        _cache = cache;
        _clock = clock;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var today = ShelterTime.Today(_clock);
        var available = await _animals.SearchAsync(new AnimalSearch(AvailableOnly: true), ct);
        var photos = await _animals.GetPrimaryPhotoIdsAsync(available.Take(4).Select(a => a.Id), ct);
        AnimalCardVm Card(Core.Entities.Animal a) => new(a, photos.TryGetValue(a.Id, out var p) ? p : null, today);

        // The public stats strip is cached for a minute. It barely changes and the home page is the busiest page.
        var stats = await _cache.GetOrCreateAsync("home-stats", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            var yearStart = new DateOnly(today.Year, 1, 1);
            var (fromUtc, toUtc) = ReportService.ToUtcRange(yearStart, today);
            var dashboard = await _reports.GetDashboardAsync(ct);
            return (Adopted: await _applications.CountAdoptedBetweenAsync(fromUtc, toUtc, ct), Dashboard: dashboard);
        });

        return View(new HomeVm
        {
            Featured = available.Count > 0 ? Card(available[0]) : null,
            ReadyToMeet = available.Skip(1).Take(3).Select(Card).ToList(),
            AdoptedThisYear = stats.Adopted,
            AnimalsInCare = stats.Dashboard.AnimalsInCare,
            ActiveVolunteers = stats.Dashboard.ActiveVolunteers,
            DonatedThisMonth = stats.Dashboard.DonationsReceivedThisMonth
        });
    }

    public async Task<IActionResult> Browse(string species = "all", string size = "all", bool available = false,
        bool kids = false, bool pets = false, string search = "", CancellationToken ct = default)
    {
        var search2 = new AnimalSearch(
            Species: Enum.TryParse<Species>(species, true, out var sp) ? sp : null,
            Size: Enum.TryParse<AnimalSize>(size, true, out var sz) ? sz : null,
            AvailableOnly: available,
            GoodWithKids: kids ? true : null,
            GoodWithOtherPets: pets ? true : null,
            Text: search.Length > 100 ? search[..100] : search);
        var animals = await _animals.SearchAsync(search2, ct);
        var photos = await _animals.GetPrimaryPhotoIdsAsync(animals.Select(a => a.Id), ct);
        var today = ShelterTime.Today(_clock);

        return View(new BrowseVm
        {
            Animals = animals.Select(a => new AnimalCardVm(a, photos.TryGetValue(a.Id, out var p) ? p : null, today)).ToList(),
            Species = search2.Species?.ToString() ?? "all",
            Size = search2.Size?.ToString() ?? "all",
            AvailableOnly = available,
            GoodWithKids = kids,
            GoodWithPets = pets,
            Search = search,
            InCareCount = await _animals.CountInCareAsync(ct)
        });
    }

    public async Task<IActionResult> Profile(Guid id, CancellationToken ct)
    {
        var animal = await _animals.GetAsync(id, ct);
        if (animal == null) return NotFound();
        return View(new ProfileVm
        {
            Animal = animal,
            Photos = await _animals.GetPhotoInfosAsync(id, ct),
            Today = ShelterTime.Today(_clock),
            IsStaff = User.IsStaff(),
            IsAdmin = User.IsInRole(Roles.Administrator)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Donate(Guid? sponsorAnimalId, CancellationToken ct)
    {
        return View(new DonateVm
        {
            Form = new DonateForm { SponsorAnimalId = sponsorAnimalId },
            SponsorableAnimals = await _animals.SearchAsync(new AnimalSearch(IncludeAdopted: false), ct)
        });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Donate([Bind(Prefix = "Form")] DonateForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _donations.PledgeAsync(User.GetUserId(),
                new DonationPledge(form.Amount, form.Frequency, form.SponsorAnimalId, form.Note), ct);
            if (result.Succeeded)
                return RedirectToAction(nameof(Receipt), new { id = result.Value!.Id });
            ModelState.AddModelError("", result.Error!);
        }
        return View(new DonateVm
        {
            Form = form,
            SponsorableAnimals = await _animals.SearchAsync(new AnimalSearch(IncludeAdopted: false), ct)
        });
    }

    [Authorize]
    public async Task<IActionResult> Receipt(Guid id, CancellationToken ct)
    {
        var receipt = await _donations.GetReceiptAsync(id, User.GetUserId(), User.IsStaff(), ct);
        return receipt == null ? NotFound() : View(receipt);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature?.Path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true)
            return Problem(title: "An unexpected error occurred.", statusCode: 500);
        ViewBag.TraceId = HttpContext.TraceIdentifier;
        return View();
    }

    [Route("/Home/StatusCode/{code:int}")]
    public IActionResult StatusCodePage(int code)
    {
        ViewBag.Code = code;
        return View("StatusCode");
    }
}
