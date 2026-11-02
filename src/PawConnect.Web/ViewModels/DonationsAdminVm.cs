using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class DonationsAdminVm
{
    public List<Donation> Pledged { get; init; } = new();
    public List<Donation> RecentlyReceived { get; init; } = new();
}
