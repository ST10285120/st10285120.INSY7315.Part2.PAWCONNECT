using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class ProfileVm
{
    public required Animal Animal { get; init; }
    public List<AnimalPhotoInfo> Photos { get; init; } = new();
    public DateOnly Today { get; init; }
    public bool IsStaff { get; init; }
    public bool IsAdmin { get; init; }
}
