using PawConnect.Core.Enums;

namespace PawConnect.Core.Services;

public record ShiftInput(
    DateTime StartsAtLocal,
    DateTime EndsAtLocal,
    string Role,
    int Capacity,
    string? Notes,
    Guid? BranchId);
