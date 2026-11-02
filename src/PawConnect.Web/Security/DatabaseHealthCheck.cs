using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PawConnect.Core.Common;
using PawConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace PawConnect.Web.Security;

/// <summary>Health check used by the deployment pipeline's smoke test and slot-swap check.</summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;
    public DatabaseHealthCheck(AppDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            if (_db.Database.IsRelational())
            {
                // A pooled "SELECT 1" is much cheaper than CanConnectAsync, which opens an unpooled connection.
                await _db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
                return HealthCheckResult.Healthy("Database reachable");
            }
            return await _db.Database.CanConnectAsync(ct)
                ? HealthCheckResult.Healthy("Database reachable")
                : HealthCheckResult.Unhealthy("Database not reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed", ex);
        }
    }
}
