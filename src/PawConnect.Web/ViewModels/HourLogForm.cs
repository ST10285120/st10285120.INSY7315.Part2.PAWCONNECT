using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class HourLogForm
{
    [Required]
    public DateOnly Date { get; set; }

    [Range(0.25, 24, ErrorMessage = "Hours must be between 0.25 and 24.")]
    public decimal Hours { get; set; }

    [Required, StringLength(60)]
    public string Activity { get; set; } = "Dog walking";

    [StringLength(500)]
    public string? Notes { get; set; }

    public static readonly string[] Activities = { "Dog walking", "Cattery cleaning", "Front desk", "Events & fundraising", "Transport", "General" };
}
