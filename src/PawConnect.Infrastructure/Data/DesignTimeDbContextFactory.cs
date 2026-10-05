using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PawConnect.Infrastructure.Data;

/// <summary>
/// Used only by the "dotnet ef" tools to create migrations. It reads the connection string from
/// the PAWCONNECT_DB environment variable, or falls back to the local docker-compose database.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PAWCONNECT_DB")
                         ?? "Host=localhost;Port=5432;Database=pawconnect;Username=pawconnect;Password=pawconnect";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        return new AppDbContext(options);
    }
}
