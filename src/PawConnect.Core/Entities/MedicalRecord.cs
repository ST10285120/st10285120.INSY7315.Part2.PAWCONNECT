using PawConnect.Core.Enums;

namespace PawConnect.Core.Entities;

/// <summary>
/// One entry in an animal's digital medical history. It replaces the paper card in the
/// kennel file and can be checked remotely by the visiting vet.
/// </summary>
public class MedicalRecord
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public Animal? Animal { get; set; }

    public Guid RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }

    public MedicalRecordType RecordType { get; set; }
    public string Title { get; set; } = "";
    public string? Details { get; set; }
    public string? VetName { get; set; }
    public DateOnly RecordDate { get; set; }
    public DateOnly? NextDueDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
