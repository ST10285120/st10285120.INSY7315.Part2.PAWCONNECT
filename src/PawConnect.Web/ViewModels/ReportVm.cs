using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Services;

namespace PawConnect.Web.ViewModels;

public class ReportVm
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public ReportSummary? Summary { get; init; }
    public string? Error { get; init; }
}
