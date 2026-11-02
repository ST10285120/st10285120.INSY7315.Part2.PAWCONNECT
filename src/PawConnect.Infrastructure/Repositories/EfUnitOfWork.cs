using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Infrastructure.Repositories;

// EF Core implementations of the Core repository interfaces. All queries go through LINQ,
// so EF Core sends parameterised SQL and user input is never concatenated into it (OWASP A03).
// Read-only queries use AsNoTracking for speed.

public class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public EfUnitOfWork(AppDbContext db) => _db = db;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The record was changed by someone else.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A unique index (e.g. one open application per animal) caught a race condition.
            throw new ConcurrencyConflictException("A conflicting record already exists.", ex);
        }
    }

    public void DiscardChanges() => _db.ChangeTracker.Clear();
}
