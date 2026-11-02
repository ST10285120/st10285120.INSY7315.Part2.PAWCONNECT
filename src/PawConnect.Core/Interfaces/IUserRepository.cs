using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

public interface IUserRepository
{
    Task<ApplicationUser?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<ApplicationUser>> GetInRoleAsync(string role, CancellationToken ct = default);
    Task<Dictionary<Guid, List<string>>> GetRolesByUserAsync(CancellationToken ct = default);
    Task<List<ApplicationUser>> GetAllAsync(CancellationToken ct = default);
}
