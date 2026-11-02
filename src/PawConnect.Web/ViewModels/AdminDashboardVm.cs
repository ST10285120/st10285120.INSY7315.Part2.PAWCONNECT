using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class AdminDashboardVm
{
    public required DashboardStats Stats { get; init; }
    public List<AdoptionApplication> RecentApplications { get; init; } = new();
    public List<MonthlyTotal> MonthlyDonations { get; init; } = new();
}
