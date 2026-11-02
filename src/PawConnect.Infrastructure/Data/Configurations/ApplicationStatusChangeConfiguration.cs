using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

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
