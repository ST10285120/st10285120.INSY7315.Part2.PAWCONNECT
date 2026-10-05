using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

// Table, key, index and relationship rules. These implement the ERD (section 5.2).
// The services validate input too, but the database has the final say: CHECK constraints and
// (partial) unique indexes keep the data valid even if a bug or a race slips past the code.

internal static class Sql
{
    /// <summary>CHECK constraint SQL limiting a string-stored enum column to its defined names.</summary>
    public static string EnumIn<TEnum>(string column) where TEnum : struct, Enum =>
        $"\"{column}\" IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"))})";
}

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(u => u.FirstName).HasMaxLength(60).IsRequired();
        b.Property(u => u.LastName).HasMaxLength(60).IsRequired();
        b.Property(u => u.VolunteerRole).HasMaxLength(60);
        b.HasOne(u => u.Branch).WithMany().HasForeignKey(u => u.BranchId).OnDelete(DeleteBehavior.SetNull);
        b.Ignore(u => u.FullName);
    }
}

public class ShelterBranchConfiguration : IEntityTypeConfiguration<ShelterBranch>
{
    public void Configure(EntityTypeBuilder<ShelterBranch> b)
    {
        b.ToTable("ShelterBranches");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Location).HasMaxLength(200);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public class AnimalConfiguration : IEntityTypeConfiguration<Animal>
{
    public void Configure(EntityTypeBuilder<Animal> b)
    {
        b.Property(a => a.Name).HasMaxLength(100).IsRequired();
        b.Property(a => a.Breed).HasMaxLength(100);
        b.Property(a => a.Bio).HasMaxLength(2000);
        b.Property(a => a.ColorFrom).HasMaxLength(7);
        b.Property(a => a.ColorTo).HasMaxLength(7);
        b.Ignore(a => a.Emoji);
        b.Property(a => a.ConcurrencyStamp).IsConcurrencyToken();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Animals_Status", Sql.EnumIn<AnimalStatus>("Status"));
            t.HasCheckConstraint("CK_Animals_Species", Sql.EnumIn<Species>("Species"));
            t.HasCheckConstraint("CK_Animals_Size", Sql.EnumIn<AnimalSize>("Size"));
            t.HasCheckConstraint("CK_Animals_KennelNumber", "\"KennelNumber\" BETWEEN 1 AND 9999");
            t.HasCheckConstraint("CK_Animals_AgeMonths", "\"AgeMonthsAtIntake\" BETWEEN 0 AND 360");
        });
        b.HasOne(a => a.Branch).WithMany().HasForeignKey(a => a.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(a => a.Photos).WithOne().HasForeignKey(p => p.AnimalId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(a => a.MedicalRecords).WithOne(m => m.Animal).HasForeignKey(m => m.AnimalId).OnDelete(DeleteBehavior.Cascade);
        // Indexes for the public browse filters.
        b.HasIndex(a => new { a.Status, a.Species });
        // One animal per kennel among the animals actually in the shelter (adopted and fostered
        // animals have left, so their kennel can be reused).
        b.HasIndex(a => new { a.BranchId, a.KennelNumber })
            .HasDatabaseName("IX_Animals_BranchId_KennelNumber_InCare")
            .IsUnique()
            .HasFilter("\"Status\" IN ('Available', 'Pending')");
    }
}

public class AnimalPhotoConfiguration : IEntityTypeConfiguration<AnimalPhoto>
{
    public void Configure(EntityTypeBuilder<AnimalPhoto> b)
    {
        b.Property(p => p.ContentType).HasMaxLength(40).IsRequired();
        b.Property(p => p.Data).IsRequired();
        b.HasIndex(p => new { p.AnimalId, p.IsPrimary });
        // At most one primary (cover) photo per animal.
        b.HasIndex(p => p.AnimalId)
            .HasDatabaseName("IX_AnimalPhotos_AnimalId_Primary")
            .IsUnique()
            .HasFilter("\"IsPrimary\"");
        b.ToTable(t => t.HasCheckConstraint("CK_AnimalPhotos_Size", "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 2097152"));
    }
}

public class AdoptionApplicationConfiguration : IEntityTypeConfiguration<AdoptionApplication>
{
    public void Configure(EntityTypeBuilder<AdoptionApplication> b)
    {
        b.Property(a => a.ReferenceNumber).HasMaxLength(20).IsRequired();
        b.HasIndex(a => a.ReferenceNumber).IsUnique();
        b.Property(a => a.FullName).HasMaxLength(120).IsRequired();
        b.Property(a => a.Phone).HasMaxLength(30).IsRequired();
        b.Property(a => a.Email).HasMaxLength(200).IsRequired();
        b.Property(a => a.Why).HasMaxLength(2000).IsRequired();
        b.Property(a => a.HomeType).HasMaxLength(60).IsRequired();
        b.Property(a => a.OwnRent).HasMaxLength(20).IsRequired();
        b.Property(a => a.OtherPets).HasMaxLength(20).IsRequired();
        b.Property(a => a.ConcurrencyStamp).IsConcurrencyToken();
        b.ToTable(t => t.HasCheckConstraint("CK_AdoptionApplications_Status", Sql.EnumIn<ApplicationStatus>("Status")));
        b.Property(a => a.Notes).HasMaxLength(2000);
        b.Property(a => a.ReviewNotes).HasMaxLength(2000);
        b.Property(a => a.HomeVisitNotes).HasMaxLength(2000);
        b.Ignore(a => a.IsActive);

        b.HasOne(a => a.Animal).WithMany().HasForeignKey(a => a.AnimalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.Adopter).WithMany().HasForeignKey(a => a.AdopterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.ReviewedBy).WithMany().HasForeignKey(a => a.ReviewedById).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(a => a.StatusHistory).WithOne().HasForeignKey(h => h.ApplicationId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(a => a.AdopterId);
        b.HasIndex(a => a.Status);
        // Database-level safety net: at most ONE open application per animal, even if two
        // people press Submit at the same instant (PostgreSQL partial unique index).
        b.HasIndex(a => a.AnimalId)
            .HasDatabaseName("IX_AdoptionApplications_AnimalId_Active")
            .IsUnique()
            .HasFilter("\"Status\" IN ('Submitted', 'UnderReview', 'Approved')");
    }
}

public class ApplicationStatusChangeConfiguration : IEntityTypeConfiguration<ApplicationStatusChange>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusChange> b)
    {
        b.Property(h => h.Note).HasMaxLength(2000);
        b.ToTable(t => t.HasCheckConstraint("CK_ApplicationStatusChanges_ToStatus", Sql.EnumIn<ApplicationStatus>("ToStatus")));
        b.HasOne(h => h.ChangedBy).WithMany().HasForeignKey(h => h.ChangedById).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(h => new { h.ApplicationId, h.ChangedAt });
    }
}

public class MedicalRecordConfiguration : IEntityTypeConfiguration<MedicalRecord>
{
    public void Configure(EntityTypeBuilder<MedicalRecord> b)
    {
        b.Property(m => m.Title).HasMaxLength(120).IsRequired();
        b.Property(m => m.Details).HasMaxLength(2000);
        b.Property(m => m.VetName).HasMaxLength(120);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MedicalRecords_Type", Sql.EnumIn<MedicalRecordType>("RecordType"));
            t.HasCheckConstraint("CK_MedicalRecords_NextDue", "\"NextDueDate\" IS NULL OR \"NextDueDate\" >= \"RecordDate\"");
        });
        b.HasOne(m => m.RecordedBy).WithMany().HasForeignKey(m => m.RecordedById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(m => new { m.AnimalId, m.RecordDate });
    }
}

public class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> b)
    {
        b.Property(d => d.Amount).HasPrecision(10, 2);
        b.Property(d => d.ReceiptNumber).HasMaxLength(30).IsRequired();
        b.Property(d => d.Note).HasMaxLength(500);
        b.HasIndex(d => d.ReceiptNumber).IsUnique();
        b.HasIndex(d => d.DonatedAt);
        b.HasIndex(d => d.DonorId);
        // One adoption fee per application, even with a double-click or two staff members.
        b.HasIndex(d => d.ApplicationId)
            .HasDatabaseName("IX_Donations_ApplicationId_AdoptionFee")
            .IsUnique()
            .HasFilter("\"Type\" = 'AdoptionFee'");
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Donations_Amount", "\"Amount\" > 0");
            t.HasCheckConstraint("CK_Donations_Type", Sql.EnumIn<DonationType>("Type"));
            t.HasCheckConstraint("CK_Donations_Status", Sql.EnumIn<DonationStatus>("Status"));
            t.HasCheckConstraint("CK_Donations_Frequency", Sql.EnumIn<DonationFrequency>("Frequency"));
            t.HasCheckConstraint("CK_Donations_AdoptionFeeHasApplication", "\"Type\" <> 'AdoptionFee' OR \"ApplicationId\" IS NOT NULL");
        });
        b.HasOne(d => d.Donor).WithMany().HasForeignKey(d => d.DonorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(d => d.Animal).WithMany().HasForeignKey(d => d.AnimalId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(d => d.Application).WithMany(a => a.Donations).HasForeignKey(d => d.ApplicationId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> b)
    {
        b.Property(s => s.Role).HasMaxLength(60).IsRequired();
        b.Property(s => s.Notes).HasMaxLength(500);
        b.Property(s => s.ConcurrencyStamp).IsConcurrencyToken();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Shifts_Capacity", "\"Capacity\" BETWEEN 1 AND 50");
            t.HasCheckConstraint("CK_Shifts_EndsAfterStart", "\"EndsAt\" > \"StartsAt\"");
        });
        b.Ignore(s => s.Filled);
        b.Ignore(s => s.IsFull);
        b.HasOne(s => s.Branch).WithMany().HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(s => s.Signups).WithOne(x => x.Shift).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(s => s.StartsAt);
    }
}

public class ShiftSignupConfiguration : IEntityTypeConfiguration<ShiftSignup>
{
    public void Configure(EntityTypeBuilder<ShiftSignup> b)
    {
        b.HasOne(x => x.Volunteer).WithMany().HasForeignKey(x => x.VolunteerId).OnDelete(DeleteBehavior.Cascade);
        // A volunteer can only hold one place on a given shift.
        b.HasIndex(x => new { x.ShiftId, x.VolunteerId }).IsUnique();
        b.HasIndex(x => x.VolunteerId);
    }
}

public class HourLogConfiguration : IEntityTypeConfiguration<HourLog>
{
    public void Configure(EntityTypeBuilder<HourLog> b)
    {
        b.Property(h => h.Hours).HasPrecision(4, 2);
        b.Property(h => h.Activity).HasMaxLength(60).IsRequired();
        b.Property(h => h.Notes).HasMaxLength(500);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_HourLogs_Hours", "\"Hours\" > 0 AND \"Hours\" <= 24");
            t.HasCheckConstraint("CK_HourLogs_Status", Sql.EnumIn<HourLogStatus>("Status"));
        });
        // Restrict: volunteer hours are an audit record and must never disappear with an account.
        b.HasOne(h => h.Volunteer).WithMany().HasForeignKey(h => h.VolunteerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(h => new { h.VolunteerId, h.Date });
        b.HasIndex(h => h.Status);
    }
}

public class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> b)
    {
        b.ToTable("Notifications");
        b.Property(n => n.Recipient).HasMaxLength(200);
        b.Property(n => n.Subject).HasMaxLength(200);
        b.Property(n => n.Body).HasMaxLength(4000);
        b.Property(n => n.Error).HasMaxLength(1000);
        b.HasIndex(n => n.CreatedAt);
    }
}
