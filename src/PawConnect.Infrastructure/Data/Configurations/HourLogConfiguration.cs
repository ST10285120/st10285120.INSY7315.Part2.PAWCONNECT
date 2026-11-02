using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

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
