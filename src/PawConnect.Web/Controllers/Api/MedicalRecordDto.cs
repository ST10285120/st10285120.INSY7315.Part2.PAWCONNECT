using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

public record MedicalRecordDto(
    Guid Id, string RecordType, string Title, string? Details, string? VetName,
    DateOnly RecordDate, DateOnly? NextDueDate, string RecordedBy, DateTime CreatedAt)
{
    public static MedicalRecordDto From(MedicalRecord m) => new(
        m.Id, m.RecordType.ToString(), m.Title, m.Details, m.VetName, m.RecordDate, m.NextDueDate,
        m.RecordedBy?.FullName ?? "", m.CreatedAt);
}
