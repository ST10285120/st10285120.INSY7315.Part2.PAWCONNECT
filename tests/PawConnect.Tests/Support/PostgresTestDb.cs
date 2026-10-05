using Microsoft.EntityFrameworkCore;
using Npgsql;
using PawConnect.Infrastructure.Data;

namespace PawConnect.Tests.Support;

/// <summary>
/// [Fact] that runs only when PAWCONNECT_TEST_POSTGRES holds a PostgreSQL connection string
/// (CI sets it to its PostgreSQL 16 service). Elsewhere the test is reported as skipped, so the
/// suite still runs on a laptop without a database.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string EnvVar = "PAWCONNECT_TEST_POSTGRES";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar)))
            Skip = $"Set {EnvVar} to a PostgreSQL connection string to run the database tests.";
    }
}

/// <summary>
/// A brand-new PostgreSQL database per test, built by the app's real EF Core migrations, so the
/// CHECK constraints, unique/partial indexes and concurrency tokens are the production ones.
/// The database is dropped afterwards.
/// </summary>
public sealed class PostgresTestDb : IDisposable
{
    public TestDb T { get; }
    private readonly DbContextOptions<AppDbContext> _options;

    public PostgresTestDb()
    {
        var server = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(PostgresFactAttribute.EnvVar))
        {
            Database = "pawconnect_test_" + Guid.NewGuid().ToString("N")[..12],
            Pooling = false
        };
        _options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(server.ConnectionString).Options;
        using (var db = new AppDbContext(_options))
            db.Database.Migrate(); // creates the database, then applies every migration
        T = new TestDb(_options);
    }

    public void Dispose()
    {
        T.Dispose();
        using var db = new AppDbContext(_options);
        db.Database.EnsureDeleted();
    }
}
