using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Core.Common;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;
using PawConnect.Core.Interfaces;
using PawConnect.Core.Notifications;
using PawConnect.Core.Receipts;
using PawConnect.Core.Services;
using PawConnect.Infrastructure.Data;
using PawConnect.Infrastructure.Repositories;

namespace PawConnect.Tests.Support;

/// <summary>A clock the tests control. Fixed at 26 Sep 2026 10:00 SAST (08:00 UTC).</summary>
public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);
}

/// <summary>Records notifications instead of sending them, so tests can assert on them.</summary>
public class CapturingNotificationService : INotificationService
{
    public List<NotificationRequest> Sent { get; } = new();
    public Task SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        Sent.Add(request);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Real repositories over the EF Core in-memory provider. Service tests use it to exercise the
/// business rules against the same data-access code the app uses, without needing PostgreSQL.
/// </summary>
public sealed class TestDb : IDisposable
{
    public AppDbContext Db { get; }
    public FakeClock Clock { get; } = new();
    public CapturingNotificationService Notifications { get; } = new();
    public IUnitOfWork Uow { get; set; }

    public ShelterBranch Branch { get; }
    public ApplicationUser Adopter { get; }
    public ApplicationUser OtherAdopter { get; }
    public ApplicationUser Volunteer { get; }
    public ApplicationUser Admin { get; }
    public Animal Dog { get; }
    public Animal Cat { get; }

    /// <summary>Name of this test's private in-memory database (lets a second context share it).</summary>
    public string DbName { get; } = "tests-" + Guid.NewGuid();

    /// <summary>Options for this test database. A second context must use the same options to share the store.</summary>
    public DbContextOptions<AppDbContext> Options { get; }

    public TestDb() : this(null) { }

    /// <param name="options">Another database to use, e.g. a real PostgreSQL one (see PostgresTestDb).
    /// The schema must already exist. Defaults to a private in-memory database.</param>
    public TestDb(DbContextOptions<AppDbContext>? options)
    {
        Options = options ?? new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(DbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        Db = new AppDbContext(Options);
        Uow = new EfUnitOfWork(Db);

        Branch = new ShelterBranch { Id = Guid.NewGuid(), Name = "Muizenberg", Location = "Cape Town" };
        Adopter = User("Jordan", "Adams", "jordan@test.local", "0791234567");
        OtherAdopter = User("Sam", "Other", "sam@test.local", null);
        Volunteer = User("Priya", "Naidoo", "priya@test.local", "0821112222", Branch.Id);
        Admin = User("Lindiwe", "Dlamini", "admin@test.local", null, Branch.Id);
        Dog = NewAnimal("Biscuit", Species.Dog, 214);
        Cat = NewAnimal("Whiskers", Species.Cat, 118);

        Db.Branches.Add(Branch);
        Db.Users.AddRange(Adopter, OtherAdopter, Volunteer, Admin);
        Db.Animals.AddRange(Dog, Cat);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    public Animal NewAnimal(string name, Species species, int kennel, AnimalStatus status = AnimalStatus.Available) => new()
    {
        Id = Guid.NewGuid(), BranchId = Branch.Id, Name = name, Species = species, KennelNumber = kennel, Status = status,
        AgeMonthsAtIntake = 24, IntakeDate = new DateOnly(2026, 8, 1), Bio = "Test animal", CreatedAt = Clock.UtcNow, UpdatedAt = Clock.UtcNow
    };

    private ApplicationUser User(string first, string last, string email, string? phone, Guid? branch = null) => new()
    {
        Id = Guid.NewGuid(), FirstName = first, LastName = last, Email = email, UserName = email, PhoneNumber = phone,
        BranchId = branch, IsActive = true, CreatedAt = Clock.UtcNow
    };

    // Repositories
    public AnimalRepository Animals => new(Db);
    public ApplicationRepository Applications => new(Db);
    public DonationRepository Donations => new(Db);
    public ShiftRepository Shifts => new(Db);
    public HourLogRepository Hours => new(Db);
    public BranchRepository Branches => new(Db);
    public UserRepository Users => new(Db);
    public MedicalRecordRepository MedicalRecords => new(Db);

    // Services wired exactly as in the app, but with the fake clock and captured notifications.
    public AdoptionService AdoptionService() => new(Applications, Animals, Donations, Uow, Notifications, Clock, NullLogger<AdoptionService>.Instance);
    public ShiftService ShiftService() => new(Shifts, Users, Branches, Uow, Notifications, Clock, NullLogger<ShiftService>.Instance);
    public HourLogService HourLogService() => new(Hours, Uow, Clock);
    public MedicalRecordService MedicalRecordService() => new(MedicalRecords, Animals, Uow, Clock);
    public DonationService DonationService() => new(Donations, Animals, Applications, Users, Uow, new DonationReceiptFactory(), Notifications, Clock);
    public AnimalService AnimalService() => new(Animals, Applications, Branches, Uow, Clock);
    public ReportService ReportService() => new(Animals, Applications, Donations, Hours, Users, Clock);

    /// <summary>
    /// Services over a SECOND, independent DbContext on the same database: a second user making
    /// a request at the same moment. Used to prove concurrency rules.
    /// </summary>
    public ServiceScope NewScope() => new(new AppDbContext(Options), Clock, Notifications);

    public static ApplicationSubmission ValidSubmission(Guid animalId) =>
        new(animalId, "Jordan Adams", "0791234567", "jordan@test.local", "We have a big garden.", "House with garden", "Own", "None", null);

    public void Dispose() => Db.Dispose();
}

/// <summary>Repositories and services over their own DbContext (see <see cref="TestDb.NewScope"/>).</summary>
public sealed class ServiceScope : IDisposable
{
    private readonly FakeClock _clock;
    private readonly CapturingNotificationService _notifications;
    public AppDbContext Db { get; }
    public IUnitOfWork Uow { get; }

    public ServiceScope(AppDbContext db, FakeClock clock, CapturingNotificationService notifications)
    {
        Db = db;
        Uow = new EfUnitOfWork(db);
        _clock = clock;
        _notifications = notifications;
    }

    public AdoptionService AdoptionService() => new(new ApplicationRepository(Db), new AnimalRepository(Db), new DonationRepository(Db), Uow, _notifications, _clock, NullLogger<AdoptionService>.Instance);
    public DonationService DonationService() => new(new DonationRepository(Db), new AnimalRepository(Db), new ApplicationRepository(Db), new UserRepository(Db), Uow, new DonationReceiptFactory(), _notifications, _clock);
    public AnimalService AnimalService() => new(new AnimalRepository(Db), new ApplicationRepository(Db), new BranchRepository(Db), Uow, _clock);
    public ShiftService ShiftService() => new(new ShiftRepository(Db), new UserRepository(Db), new BranchRepository(Db), Uow, _notifications, _clock, NullLogger<ShiftService>.Instance);

    public void Dispose() => Db.Dispose();
}
