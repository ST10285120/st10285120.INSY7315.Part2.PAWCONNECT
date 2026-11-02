using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class VolunteerDashboardVm
{
    public string FirstName { get; init; } = "";
    public List<AdoptionApplication> OpenApplications { get; init; } = new();
    public List<ShiftRowVm> Shifts { get; init; } = new();
    public List<HourLog> MyHours { get; init; } = new();
    public decimal MyApprovedTotal { get; init; }
    public HourLogForm HourForm { get; init; } = new();
    public bool IsAdmin { get; init; }
}
