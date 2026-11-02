using PawConnect.Core.Enums;

namespace PawConnect.Core.Services;

public record HourLogInput(DateOnly Date, decimal Hours, string Activity, string? Notes);
