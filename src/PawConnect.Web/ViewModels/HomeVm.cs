using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class HomeVm
{
    public AnimalCardVm? Featured { get; init; }
    public List<AnimalCardVm> ReadyToMeet { get; init; } = new();
    public int AdoptedThisYear { get; init; }
    public int AnimalsInCare { get; init; }
    public int ActiveVolunteers { get; init; }
    public decimal DonatedThisMonth { get; init; }
}
