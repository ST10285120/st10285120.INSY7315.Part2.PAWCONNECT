using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

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
