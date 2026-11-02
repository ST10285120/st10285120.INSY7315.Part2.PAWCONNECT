using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class ShiftForm
{
    [Required, Display(Name = "Date")]
    public DateOnly Date { get; set; }

    [Required, Display(Name = "Start time")]
    public TimeOnly StartTime { get; set; } = new(9, 0);

    [Required, Display(Name = "End time")]
    public TimeOnly EndTime { get; set; } = new(12, 0);

    [Required, StringLength(60)]
    public string Role { get; set; } = "Dog walking";

    [Range(1, 50)]
    public int Capacity { get; set; } = 3;

    [StringLength(500)]
    public string? Notes { get; set; }
}
