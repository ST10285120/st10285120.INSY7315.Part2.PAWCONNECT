using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

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
