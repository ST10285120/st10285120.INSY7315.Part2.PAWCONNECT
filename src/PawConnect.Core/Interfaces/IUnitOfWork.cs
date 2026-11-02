using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

/// <summary>Commits all repository changes made during a request as one unit.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Forgets tracked (possibly stale) entities, e.g. before retrying after a concurrency conflict.</summary>
    void DiscardChanges();
}
