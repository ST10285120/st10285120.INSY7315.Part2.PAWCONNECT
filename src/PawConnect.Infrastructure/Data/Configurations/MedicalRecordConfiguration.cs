using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Infrastructure.Data.Configurations;

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
