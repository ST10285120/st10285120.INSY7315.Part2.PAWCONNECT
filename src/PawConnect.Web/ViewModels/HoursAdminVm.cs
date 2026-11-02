using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class HoursAdminVm
{
    public List<HourLog> Logs { get; init; } = new();
    public List<ApplicationUser> Volunteers { get; init; } = new();
    public Guid? SelectedVolunteerId { get; init; }
    public string SelectedStatus { get; init; } = "all";
    public decimal ApprovedThisMonth { get; init; }
    public int PendingCount { get; init; }
    public List<(string Name, decimal Hours)> ChartData { get; init; } = new();
}
