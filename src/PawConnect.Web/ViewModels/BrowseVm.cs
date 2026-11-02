using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class BrowseVm
{
    public List<AnimalCardVm> Animals { get; init; } = new();
    public string Species { get; init; } = "all";
    public string Size { get; init; } = "all";
    public bool AvailableOnly { get; init; }
    public bool GoodWithKids { get; init; }
    public bool GoodWithPets { get; init; }
    public string Search { get; init; } = "";
    public int InCareCount { get; init; }
}
