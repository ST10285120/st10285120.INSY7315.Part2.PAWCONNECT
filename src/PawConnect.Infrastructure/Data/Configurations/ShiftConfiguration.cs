using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

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
