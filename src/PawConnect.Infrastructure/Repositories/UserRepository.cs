using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    public UserRepository(AppDbContext db) => _db = db;

    public Task<ApplicationUser?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<List<ApplicationUser>> GetInRoleAsync(string role, CancellationToken ct = default) =>
        (from u in _db.Users.AsNoTracking()
         join ur in _db.UserRoles on u.Id equals ur.UserId
         join r in _db.Roles on ur.RoleId equals r.Id
         where r.Name == role
         orderby u.FirstName, u.LastName
         select u).ToListAsync(ct);

    public async Task<Dictionary<Guid, List<string>>> GetRolesByUserAsync(CancellationToken ct = default)
    {
        var rows = await (from ur in _db.UserRoles
                          join r in _db.Roles on ur.RoleId equals r.Id
                          select new { ur.UserId, r.Name }).ToListAsync(ct);
        return rows.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Name!).OrderBy(n => n).ToList());
    }

    public Task<List<ApplicationUser>> GetAllAsync(CancellationToken ct = default) =>
        _db.Users.AsNoTracking().Include(u => u.Branch).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync(ct);
}
