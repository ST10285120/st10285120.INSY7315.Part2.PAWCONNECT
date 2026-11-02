using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class AnimalForm
{
    public Guid? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    public Species Species { get; set; } = Species.Dog;

    [StringLength(100)]
    public string? Breed { get; set; }

    [Range(0, 360, ErrorMessage = "Age must be between 0 and 360 months."), Display(Name = "Age (months)")]
    public int AgeMonths { get; set; } = 12;

    public AnimalSize Size { get; set; } = AnimalSize.Medium;

    [Display(Name = "Good with kids")]
    public bool GoodWithKids { get; set; }

    [Display(Name = "Good with other pets")]
    public bool GoodWithOtherPets { get; set; }

    [Display(Name = "Vaccinated")]
    public bool IsVaccinated { get; set; }

    [Range(1, 9999), Display(Name = "Kennel number")]
    public int KennelNumber { get; set; }

    [StringLength(2000)]
    public string? Bio { get; set; }

    [Display(Name = "Intake date")]
    public DateOnly? IntakeDate { get; set; }

    public Guid? BranchId { get; set; }

    public AnimalInput ToInput() => new(Name, Species, Breed, AgeMonths, Size, GoodWithKids, GoodWithOtherPets,
        IsVaccinated, KennelNumber, Bio ?? "", IntakeDate, BranchId);

    public static AnimalForm From(Animal a) => new()
    {
        Id = a.Id, Name = a.Name, Species = a.Species, Breed = a.Breed, AgeMonths = a.AgeMonthsAtIntake, Size = a.Size,
        GoodWithKids = a.GoodWithKids, GoodWithOtherPets = a.GoodWithOtherPets, IsVaccinated = a.IsVaccinated,
        KennelNumber = a.KennelNumber, Bio = a.Bio, IntakeDate = a.IntakeDate, BranchId = a.BranchId
    };
}
