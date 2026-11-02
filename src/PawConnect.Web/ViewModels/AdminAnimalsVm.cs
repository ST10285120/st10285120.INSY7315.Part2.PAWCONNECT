using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class AdminAnimalsVm
{
    public List<Animal> Animals { get; init; } = new();
    public AnimalForm NewAnimal { get; init; } = new();
    public List<ShelterBranch> Branches { get; init; } = new();
}
