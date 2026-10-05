using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;
using PawConnect.Web.Security;
using PawConnect.Web.ViewModels;

namespace PawConnect.Web.Controllers;

/// <summary>The adopter's side of the adoption process: apply, confirmation, track, withdraw.</summary>
[Authorize(Roles = Roles.Adopter)]
public class ApplicationsController : Controller
{
    private readonly AdoptionService _adoptions;
    private readonly IAnimalRepository _animals;
    private readonly IApplicationRepository _applications;
    private readonly UserManager<ApplicationUser> _users;

    public ApplicationsController(AdoptionService adoptions, IAnimalRepository animals,
        IApplicationRepository applications, UserManager<ApplicationUser> users)
    {
        _adoptions = adoptions;
        _animals = animals;
        _applications = applications;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Apply(Guid animalId, CancellationToken ct)
    {
        var animal = await _animals.GetAsync(animalId, ct);
        if (animal == null) return NotFound();
        if (animal.Status != AnimalStatus.Available)
        {
            TempData["Toast"] = $"{animal.Name} is not available for adoption right now.";
            return RedirectToAction("Profile", "Home", new { id = animalId });
        }

        // Pre-fill contact details from the account to keep the form short (usability NFR).
        var user = await _users.GetUserAsync(User);
        var form = new ApplicationForm
        {
            AnimalId = animalId,
            FullName = user?.FullName ?? "",
            Email = user?.Email ?? "",
            Phone = user?.PhoneNumber ?? ""
        };
        return View(new ApplyVm { Animal = animal, Form = form });
    }

    [HttpPost]
    public async Task<IActionResult> Submit([Bind(Prefix = "Form")] ApplicationForm form, CancellationToken ct)
    {
        var animal = await _animals.GetAsync(form.AnimalId, ct);
        if (animal == null) return NotFound();

        if (ModelState.IsValid)
        {
            var result = await _adoptions.SubmitAsync(User.GetUserId(), form.ToSubmission(), ct);
            if (result.Succeeded)
                return RedirectToAction(nameof(Confirmation), new { id = result.Value!.Id });
            ModelState.AddModelError("", result.Error!);
        }
        return View("Apply", new ApplyVm { Animal = animal, Form = form });
    }

    public async Task<IActionResult> Confirmation(Guid id, CancellationToken ct)
    {
        var app = await _applications.GetAsync(id, ct);
        if (app == null || app.AdopterId != User.GetUserId()) return NotFound();
        return View(app);
    }

    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var applications = await _applications.GetForAdopterAsync(User.GetUserId(), ct);
        ViewBag.PhotoIds = await _animals.GetPrimaryPhotoIdsAsync(applications.Select(a => a.AnimalId).Distinct(), ct);
        return View(applications);
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        var result = await _adoptions.WithdrawAsync(id, User.GetUserId(), ct);
        TempData["Toast"] = result.Succeeded ? "Your application has been withdrawn." : result.Error;
        return RedirectToAction(nameof(Mine));
    }
}
