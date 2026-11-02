using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Infrastructure.Repositories;

public class AnimalRepository : IAnimalRepository
{
    private readonly AppDbContext _db;
    public AnimalRepository(AppDbContext db) => _db = db;

    public async Task<List<Animal>> SearchAsync(AnimalSearch s, CancellationToken ct = default)
    {
        var q = _db.Animals.AsNoTracking().AsQueryable();
        if (s.Species is { } species) q = q.Where(a => a.Species == species);
        if (s.Size is { } size) q = q.Where(a => a.Size == size);
        if (s.AvailableOnly) q = q.Where(a => a.Status == AnimalStatus.Available);
        else if (!s.IncludeAdopted) q = q.Where(a => a.Status != AnimalStatus.Adopted);
        if (s.GoodWithKids == true) q = q.Where(a => a.GoodWithKids);
        if (s.GoodWithOtherPets == true) q = q.Where(a => a.GoodWithOtherPets);
        if (s.BranchId is { } branchId) q = q.Where(a => a.BranchId == branchId);
        if (!string.IsNullOrWhiteSpace(s.Text))
        {
            var text = s.Text.Trim().ToLower();
            q = q.Where(a => a.Name.ToLower().Contains(text) || (a.Breed != null && a.Breed.ToLower().Contains(text)));
        }

        // Available animals first, then pending/fostered, adopted last; newest arrivals first within each.
        return await q
            .OrderBy(a => a.Status == AnimalStatus.Available ? 0 : a.Status == AnimalStatus.Adopted ? 2 : 1)
            .ThenByDescending(a => a.IntakeDate)
            .ThenBy(a => a.Name)
            .ToListAsync(ct);
    }

    public Task<Animal?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Animals.Include(a => a.Branch).FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Animal?> GetWithMedicalHistoryAsync(Guid id, CancellationToken ct = default) =>
        _db.Animals.AsNoTracking()
            .Include(a => a.MedicalRecords.OrderByDescending(m => m.RecordDate)).ThenInclude(m => m.RecordedBy)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<int> CountInCareAsync(CancellationToken ct = default) =>
        _db.Animals.CountAsync(a => a.Status != AnimalStatus.Adopted, ct);

    public Task<bool> KennelInUseAsync(Guid branchId, int kennelNumber, Guid? exceptAnimalId, CancellationToken ct = default) =>
        _db.Animals.AnyAsync(a => a.BranchId == branchId && a.KennelNumber == kennelNumber
                                  && a.Status != AnimalStatus.Adopted && a.Status != AnimalStatus.Fostered
                                  && a.Id != exceptAnimalId, ct);

    public async Task AddAsync(Animal animal, CancellationToken ct = default) => await _db.Animals.AddAsync(animal, ct);

    public async Task<Dictionary<Guid, Guid>> GetPrimaryPhotoIdsAsync(IEnumerable<Guid> animalIds, CancellationToken ct = default)
    {
        var ids = animalIds.Distinct().ToList();
        // Projection: only the ids are read, never the image bytes.
        var rows = await _db.AnimalPhotos.AsNoTracking()
            .Where(p => ids.Contains(p.AnimalId) && p.IsPrimary)
            .Select(p => new { p.AnimalId, p.Id })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.AnimalId).ToDictionary(g => g.Key, g => g.First().Id);
    }

    public Task<List<AnimalPhotoInfo>> GetPhotoInfosAsync(Guid animalId, CancellationToken ct = default) =>
        _db.AnimalPhotos.AsNoTracking()
            .Where(p => p.AnimalId == animalId)
            .OrderByDescending(p => p.IsPrimary).ThenBy(p => p.UploadedAt)
            .Select(p => new AnimalPhotoInfo(p.Id, p.AnimalId, p.IsPrimary, p.UploadedAt))
            .ToListAsync(ct);

    public Task<AnimalPhoto?> GetPhotoAsync(Guid photoId, CancellationToken ct = default) =>
        _db.AnimalPhotos.FirstOrDefaultAsync(p => p.Id == photoId, ct);

    public async Task AddPhotoAsync(AnimalPhoto photo, CancellationToken ct = default) => await _db.AnimalPhotos.AddAsync(photo, ct);

    public void RemovePhoto(AnimalPhoto photo) => _db.AnimalPhotos.Remove(photo);
}
