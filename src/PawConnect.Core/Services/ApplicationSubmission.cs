using PawConnect.Core.Enums;

namespace PawConnect.Core.Services;

// Plain input records passed from the web layer (MVC forms or the REST API) to the services.
// The services validate them again, so the rules hold however the data arrives.

public record ApplicationSubmission(
    Guid AnimalId,
    string FullName,
    string Phone,
    string Email,
    string Why,
    string HomeType,
    string OwnRent,
    string OtherPets,
    string? Notes);
