using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data;

/// <summary>
/// EF Core context for PostgreSQL. Identity tables (users, roles, claims) come from
/// IdentityDbContext; the shelter tables are configured in the Configurations folder.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ShelterBranch> Branches => Set<ShelterBranch>();
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<AnimalPhoto> AnimalPhotos => Set<AnimalPhoto>();
    public DbSet<AdoptionApplication> AdoptionApplications => Set<AdoptionApplication>();
    public DbSet<ApplicationStatusChange> ApplicationStatusChanges => Set<ApplicationStatusChange>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftSignup> ShiftSignups => Set<ShiftSignup>();
    public DbSet<HourLog> HourLogs => Set<HourLog>();
    public DbSet<NotificationMessage> Notifications => Set<NotificationMessage>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as readable text ("UnderReview") rather than numbers, matching the
        // ENUM columns in the data dictionary and keeping the database easy to query by hand.
        configurationBuilder.Properties<Species>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<AnimalSize>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<AnimalStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ApplicationStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<MedicalRecordType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DonationType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DonationFrequency>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DonationStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<HourLogStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<NotificationChannel>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<NotificationUrgency>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<NotificationStatus>().HaveConversion<string>().HaveMaxLength(20);
    }
}
