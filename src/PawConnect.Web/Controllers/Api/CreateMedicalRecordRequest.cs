using System.ComponentModel.DataAnnotations;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Services;

namespace PawConnect.Web.Controllers.Api;

public class CreateMedicalRecordRequest
{
    [Required] public Guid AnimalId { get; set; }
    [Required] public MedicalRecordType RecordType { get; set; }
    [Required, StringLength(120)] public string Title { get; set; } = "";
    [StringLength(2000)] public string? Details { get; set; }
    [StringLength(120)] public string? VetName { get; set; }
    [Required] public DateOnly RecordDate { get; set; }
    public DateOnly? NextDueDate { get; set; }

    public MedicalRecordInput ToInput() => new(RecordType, Title, Details, VetName, RecordDate, NextDueDate);
}
