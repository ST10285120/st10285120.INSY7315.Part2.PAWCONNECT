using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

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
