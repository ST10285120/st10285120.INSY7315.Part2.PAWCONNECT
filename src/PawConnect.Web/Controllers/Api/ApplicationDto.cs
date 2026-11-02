using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

public record ApplicationDto(
    Guid Id, string ReferenceNumber, Guid AnimalId, string? AnimalName, string Status, DateTime SubmittedAt,
    DateTime? DecidedAt, string? ReviewNotes, bool HomeVisitCompleted)
{
    public static ApplicationDto From(AdoptionApplication a) => new(
        a.Id, a.ReferenceNumber, a.AnimalId, a.Animal?.Name, a.Status.ToString(), a.SubmittedAt, a.DecidedAt,
        a.ReviewNotes, a.HomeVisitCompletedAt != null);
}
