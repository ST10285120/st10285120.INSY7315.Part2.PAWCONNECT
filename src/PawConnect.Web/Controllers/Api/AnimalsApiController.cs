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
