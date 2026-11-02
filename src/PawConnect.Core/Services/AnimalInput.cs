using PawConnect.Core.Enums;

namespace PawConnect.Core.Services;

public record AnimalInput(
    string Name,
    Species Species,
    string? Breed,
    int AgeMonths,
    AnimalSize Size,
    bool GoodWithKids,
    bool GoodWithOtherPets,
    bool IsVaccinated,
    int KennelNumber,
    string Bio,
    DateOnly? IntakeDate,
    Guid? BranchId);
