using Microsoft.AspNetCore.Mvc;
using PawConnect.Core.Interfaces;

namespace PawConnect.Web.Controllers;

/// <summary>Serves animal photos from the database with long-lived caching (photos never change once uploaded).</summary>
public class MediaController : Controller
{
    private readonly IAnimalRepository _animals;
    public MediaController(IAnimalRepository animals) => _animals = animals;

    [HttpGet("/media/photos/{id:guid}")]
    [ResponseCache(Duration = 60 * 60 * 24 * 30, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Photo(Guid id, CancellationToken ct)
    {
        var photo = await _animals.GetPhotoAsync(id, ct);
        if (photo == null) return NotFound();
        return File(photo.Data, photo.ContentType);
    }
}
