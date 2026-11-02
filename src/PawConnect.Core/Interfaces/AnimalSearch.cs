using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

// Repository pattern (design doc, section 6.1): every entity has a repository that hides
// the database queries. Services and controllers only see these interfaces, so business
// logic can be unit tested without a real PostgreSQL server.

public record AnimalSearch(
    Species? Species = null,
    AnimalSize? Size = null,
    bool AvailableOnly = false,
    bool? GoodWithKids = null,
    bool? GoodWithOtherPets = null,
    string? Text = null,
    Guid? BranchId = null,
    bool IncludeAdopted = true);
