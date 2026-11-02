using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

public record ShiftDto(Guid Id, DateTime StartsAt, DateTime EndsAt, string Role, int Capacity, int Filled, bool IsFull, bool SignedUp)
{
    public static ShiftDto From(Shift s, Guid userId) =>
        new(s.Id, s.StartsAt, s.EndsAt, s.Role, s.Capacity, s.Filled, s.IsFull, s.Signups.Any(x => x.VolunteerId == userId));
}
