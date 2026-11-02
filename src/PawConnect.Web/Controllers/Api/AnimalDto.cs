using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

// Data transfer objects: the JSON shapes the API sends and receives. Entities are never
// serialised directly, so internal fields (e.g. password hashes, photo bytes) can't leak.

public record AnimalDto(
    Guid Id, string Name, string Species, string? Breed, string Age, int AgeMonths, string Size, string Status,
    bool IsVaccinated, bool GoodWithKids, bool GoodWithOtherPets, int KennelNumber, string Bio,
    string? PhotoUrl, string ColorFrom, string ColorTo, string Emoji, string ProfileUrl)
{
    public static AnimalDto From(Animal a, Guid? photoId, DateOnly today) => new(
        a.Id, a.Name, a.Species.ToString(), a.Breed, a.AgeLabel(today), a.CurrentAgeMonths(today), a.Size.ToString(),
        a.Status.ToString(), a.IsVaccinated, a.GoodWithKids, a.GoodWithOtherPets, a.KennelNumber, a.Bio,
        photoId is { } p ? $"/media/photos/{p}" : null, a.ColorFrom, a.ColorTo, a.Emoji, $"/Home/Profile/{a.Id}");
}
