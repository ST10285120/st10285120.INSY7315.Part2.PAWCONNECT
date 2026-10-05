using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PawConnect.Infrastructure.Data;
using PawConnect.Tests.Support;

namespace PawConnect.Tests.Database;

/// <summary>
/// Upgrading a LIVE database: the integrity migration must work on existing data, fix what it
/// safely can, and refuse (leaving the database untouched) when a person has to decide.
/// </summary>
public class MigrationUpgradeTests
{
    private const string BeforeIntegrity = "20260926081447_DataProtectionKeys";

    private sealed class OldSchemaDb : IDisposable
    {
        public AppDbContext Db { get; }
        public Guid Branch { get; } = Guid.NewGuid();

        public OldSchemaDb()
        {
            var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(PostgresFactAttribute.EnvVar))
            {
                Database = "pawconnect_upgrade_" + Guid.NewGuid().ToString("N")[..12],
                Pooling = false
            };
            Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs.ConnectionString).Options);
            Db.GetService<IMigrator>().Migrate(BeforeIntegrity); // the schema as it was before the upgrade
            Db.Database.ExecuteSqlRaw("INSERT INTO \"ShelterBranches\" (\"Id\", \"Name\", \"Location\", \"IsActive\") VALUES ({0}, 'Muizenberg', 'Cape Town', true)", Branch);
        }

        public Guid AddAnimal(int kennel, string status)
        {
            var id = Guid.NewGuid();
            Db.Database.ExecuteSqlRaw(@"INSERT INTO ""Animals"" (""Id"", ""BranchId"", ""Name"", ""Species"", ""AgeMonthsAtIntake"", ""Size"",
                ""GoodWithKids"", ""GoodWithOtherPets"", ""Status"", ""IsVaccinated"", ""KennelNumber"", ""Bio"", ""IntakeDate"",
                ""ColorFrom"", ""ColorTo"", ""CreatedAt"", ""UpdatedAt"")
                VALUES ({0}, {1}, 'Rex', 'Dog', 12, 'Medium', true, true, {2}, true, {3}, '', '2026-08-01', '#000000', '#ffffff', now(), now())",
                id, Branch, status, kennel);
            return id;
        }

        public void AddPhoto(Guid animal, bool primary, int minutesAgo) =>
            Db.Database.ExecuteSqlRaw(@"INSERT INTO ""AnimalPhotos"" (""Id"", ""AnimalId"", ""ContentType"", ""Data"", ""SizeBytes"", ""IsPrimary"", ""UploadedAt"")
                VALUES ({0}, {1}, 'image/jpeg', '\xffd8ff'::bytea, 3, {2}, now() - make_interval(mins => {3}))", Guid.NewGuid(), animal, primary, minutesAgo);

        public void Dispose()
        {
            Db.Database.EnsureDeleted();
            Db.Dispose();
        }
    }

    [PostgresFact]
    public async Task Upgrade_keeps_existing_data_and_repairs_duplicate_primary_photos()
    {
        using var old = new OldSchemaDb();
        var rex = old.AddAnimal(10, "Available");
        old.AddAnimal(10, "Adopted"); // an adopted animal's old kennel: allowed
        old.AddPhoto(rex, primary: true, minutesAgo: 60);
        old.AddPhoto(rex, primary: true, minutesAgo: 5); // left behind by an old race condition

        old.Db.Database.Migrate();

        Assert.Empty(old.Db.Database.GetPendingMigrations());
        Assert.Equal(2, await old.Db.Animals.CountAsync());
        Assert.Equal(1, await old.Db.AnimalPhotos.CountAsync(p => p.AnimalId == rex && p.IsPrimary));
        Assert.DoesNotContain(await old.Db.Animals.Select(a => a.ConcurrencyStamp).ToListAsync(), s => s == Guid.Empty);
    }

    [PostgresFact]
    public void Upgrade_stops_with_a_clear_message_when_two_animals_in_care_share_a_kennel()
    {
        using var old = new OldSchemaDb();
        old.AddAnimal(7, "Available");
        old.AddAnimal(7, "Pending");

        var ex = Assert.ThrowsAny<Exception>(() => old.Db.Database.Migrate());
        Assert.Contains("kennel 7", ex.Message);
        // Nothing was half-applied: the database is still on the old version and can be fixed and retried.
        Assert.Single(old.Db.Database.GetPendingMigrations());
    }
}
