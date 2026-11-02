using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

public interface IAnimalRepository
{
    Task<List<Animal>> SearchAsync(AnimalSearch search, CancellationToken ct = default);
    Task<Animal?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Animal?> GetWithMedicalHistoryAsync(Guid id, CancellationToken ct = default);
    Task<int> CountInCareAsync(CancellationToken ct = default);
    Task<bool> KennelInUseAsync(Guid branchId, int kennelNumber, Guid? exceptAnimalId, CancellationToken ct = default);
    Task AddAsync(Animal animal, CancellationToken ct = default);

    Task<Dictionary<Guid, Guid>> GetPrimaryPhotoIdsAsync(IEnumerable<Guid> animalIds, CancellationToken ct = default);
    Task<List<AnimalPhotoInfo>> GetPhotoInfosAsync(Guid animalId, CancellationToken ct = default);
    Task<AnimalPhoto?> GetPhotoAsync(Guid photoId, CancellationToken ct = default);
    Task AddPhotoAsync(AnimalPhoto photo, CancellationToken ct = default);
    void RemovePhoto(AnimalPhoto photo);
}
