using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class UsersAdminVm
{
    public List<UserRowVm> Users { get; init; } = new();
    public CreateUserForm NewUser { get; init; } = new();
    public string Filter { get; init; } = "all";
    public Guid CurrentUserId { get; init; }
    public List<ShelterBranch> Branches { get; init; } = new();
}
