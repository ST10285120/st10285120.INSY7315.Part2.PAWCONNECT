using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

public interface IMedicalRecordRepository
{
    Task<List<MedicalRecord>> GetForAnimalAsync(Guid animalId, CancellationToken ct = default);
    Task<MedicalRecord?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(MedicalRecord record, CancellationToken ct = default);
}
