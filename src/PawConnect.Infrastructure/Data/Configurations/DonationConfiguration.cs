using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

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
