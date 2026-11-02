using PawConnect.Core.Enums;

namespace PawConnect.Core.Services;

public record MedicalRecordInput(
    MedicalRecordType RecordType,
    string Title,
    string? Details,
    string? VetName,
    DateOnly RecordDate,
    DateOnly? NextDueDate);
