using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Infrastructure.Repositories;

public class MedicalRecordRepository : IMedicalRecordRepository
{
    private readonly AppDbContext _db;
    public MedicalRecordRepository(AppDbContext db) => _db = db;

    public Task<List<MedicalRecord>> GetForAnimalAsync(Guid animalId, CancellationToken ct = default) =>
        _db.MedicalRecords.AsNoTracking()
            .Include(m => m.RecordedBy)
            .Where(m => m.AnimalId == animalId)
            .OrderByDescending(m => m.RecordDate).ThenByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

    public Task<MedicalRecord?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.MedicalRecords.AsNoTracking().Include(m => m.RecordedBy).FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task AddAsync(MedicalRecord record, CancellationToken ct = default) => await _db.MedicalRecords.AddAsync(record, ct);
}
