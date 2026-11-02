using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

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
