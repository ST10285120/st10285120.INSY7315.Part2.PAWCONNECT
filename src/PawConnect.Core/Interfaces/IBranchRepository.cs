using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

public interface IBranchRepository
{
    Task<List<ShelterBranch>> GetAllAsync(CancellationToken ct = default);
    Task<ShelterBranch?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ShelterBranch> GetDefaultAsync(CancellationToken ct = default);
    Task AddAsync(ShelterBranch branch, CancellationToken ct = default);
}
