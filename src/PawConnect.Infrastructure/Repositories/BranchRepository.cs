using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Infrastructure.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly AppDbContext _db;
    public BranchRepository(AppDbContext db) => _db = db;

    public Task<List<ShelterBranch>> GetAllAsync(CancellationToken ct = default) =>
        _db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync(ct);

    public Task<ShelterBranch?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Branches.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<ShelterBranch> GetDefaultAsync(CancellationToken ct = default) =>
        await _db.Branches.Where(b => b.IsActive).OrderBy(b => b.Name).FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException("No shelter branch exists. Run the database seeder.");

    public async Task AddAsync(ShelterBranch branch, CancellationToken ct = default) => await _db.Branches.AddAsync(branch, ct);
}
